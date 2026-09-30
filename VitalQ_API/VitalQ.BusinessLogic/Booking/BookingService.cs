using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

public class BookingService : IBookingService
{
    private readonly VitalQDbContext _context;
    private readonly IMemoryCache _cache;

    public BookingService(VitalQDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // =========================================================================
    // 1. GET ACTIVE DEPARTMENTS (Cached for 2 min to shield DB under 100k load)
    // =========================================================================
    public async Task<IEnumerable<DepartmentResponse>> GetActiveDepartmentsAsync()
    {
        return await _cache.GetOrCreateAsync("ActiveDepartments", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await _context.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentResponse
                {
                    Id = d.Id,
                    Code = d.Code,
                    Name = d.Name,
                    LocationFloor = d.LocationFloor,
                    LastTokenNumber = d.LastTokenNumber,
                    IsActive = d.IsActive,
                    CreatedAtUtc = d.CreatedAtUtc
                })
                .ToListAsync();
        }) ?? Enumerable.Empty<DepartmentResponse>();
    }

    // =========================================================================
    // 2. GET AVAILABLE DOCTORS BY DEPARTMENT (Cached for 1 min)
    // =========================================================================
    public async Task<IEnumerable<DoctorResponse>> GetAvailableDoctorsAsync(Guid departmentId)
    {
        var cacheKey = $"AvailableDoctors_{departmentId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return await _context.Doctors
                .AsNoTracking()
                .Where(d => d.DepartmentId == departmentId && d.Status == "Available")
                .Include(d => d.User)
                .Include(d => d.Department)
                .Select(d => new DoctorResponse
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    DoctorName = d.User.FullName,
                    Username = d.User.Username,
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.Department.Name,
                    Specialization = d.Specialization,
                    RoomNumber = d.RoomNumber,
                    Status = d.Status,
                    AvgConsultationMinutes = d.AvgConsultationMinutes,
                    CreatedAtUtc = d.CreatedAtUtc
                })
                .ToListAsync();
        }) ?? Enumerable.Empty<DoctorResponse>();
    }

    // =========================================================================
    // 3. BOOK AN APPOINTMENT SLOT (Simple & Concurrency-Safe)
    // =========================================================================
    public async Task<QueueTokenResponse> BookTokenAsync(BookTokenRequest request)
    {
        // Step A: Find Patient (Read-only AsNoTracking)
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PatientId)
            ?? throw new KeyNotFoundException("Patient profile was not found.");

        // Step B: Find & Validate Doctor (Read-only AsNoTracking)
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.Department)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == request.DoctorId)
            ?? throw new KeyNotFoundException("Doctor was not found.");

        if (doctor.Status != "Available")
            throw new InvalidOperationException($"Dr. {doctor.User.FullName} is currently not available ({doctor.Status}).");

        if (doctor.DepartmentId != request.DepartmentId)
            throw new ArgumentException("The doctor does not belong to the selected department.");

        // Step C: Anti-Double Booking Check for today
        var today = DateTime.UtcNow.Date;
        var alreadyBooked = await _context.QueueTokens
            .AsNoTracking()
            .AnyAsync(t => t.PatientId == patient.Id
                        && t.DoctorId == doctor.Id
                        && t.BookedAtUtc >= today
                        && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"));

        if (alreadyBooked)
        {
            throw new InvalidOperationException(
                $"Patient '{patient.FullName}' already has an active appointment with Dr. {doctor.User.FullName} today.");
        }

        // Step D: Generate sequential token (e.g. CARD-001)
        var nextNumber = await GetNextTokenNumberAsync(doctor.DepartmentId);
        var tokenNumber = $"{doctor.Department.Code.Trim().ToUpper()}-{nextNumber:D3}";

        // Step E: Save Token & Audit Log
        var queueToken = new QueueToken
        {
            Id = Guid.NewGuid(),
            TokenNumber = tokenNumber,
            PatientId = patient.Id,
            DepartmentId = doctor.DepartmentId,
            DoctorId = doctor.Id,
            Status = "Booked",
            BaseWeight = 0,
            PriorityScore = 0,
            BookedAtUtc = DateTime.UtcNow
        };

        var auditLog = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = queueToken.Id,
            PreviousStatus = "None",
            NewStatus = "Booked",
            ChangeSource = "User",
            ChangedAtUtc = DateTime.UtcNow,
            Notes = $"Token {tokenNumber} booked for {patient.FullName}"
        };

        await _context.QueueTokens.AddAsync(queueToken);
        await _context.TokenAuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();

        return MapToResponse(queueToken, patient, doctor);
    }

    // =========================================================================
    // 4. GET PATIENT ACTIVE TOKEN TODAY
    // =========================================================================
    public async Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId)
    {
        var today = DateTime.UtcNow.Date;

        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .Where(t => t.PatientId == patientId
                     && t.BookedAtUtc >= today
                     && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"))
            .OrderByDescending(t => t.BookedAtUtc)
            .FirstOrDefaultAsync();

        if (token == null) return null;

        return MapToResponse(token, token.Patient, token.Doctor);
    }

    // =========================================================================
    // HELPER: ATOMIC TOKEN COUNTER (Simple & Fast)
    // =========================================================================
    private async Task<int> GetNextTokenNumberAsync(Guid departmentId)
    {
        await using var cmd = _context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "UPDATE Departments SET LastTokenNumber = LastTokenNumber + 1 OUTPUT INSERTED.LastTokenNumber WHERE Id = @deptId";

        var param = cmd.CreateParameter();
        param.ParameterName = "@deptId";
        param.Value = departmentId;
        cmd.Parameters.Add(param);

        if (cmd.Connection!.State != ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // Map database entities to response DTO
    private static QueueTokenResponse MapToResponse(QueueToken t, Patient p, Doctor d) => new()
    {
        Id = t.Id,
        TokenNumber = t.TokenNumber,
        PatientId = p.Id,
        PatientName = p.FullName,
        PatientPhone = p.PhoneNumber,
        DepartmentId = d.DepartmentId,
        DepartmentName = d.Department?.Name ?? string.Empty,
        DepartmentCode = d.Department?.Code ?? string.Empty,
        DoctorId = d.Id,
        DoctorName = d.User?.FullName ?? string.Empty,
        RoomNumber = d.RoomNumber ?? string.Empty,
        Status = t.Status,
        PriorityScore = t.PriorityScore,
        BaseWeight = t.BaseWeight,
        TriageLevel = t.TriageAssessment?.TriageLevel,
        BookedAtUtc = t.BookedAtUtc,
        RowVersion = t.RowVersion ?? Array.Empty<byte>()
    };
}
