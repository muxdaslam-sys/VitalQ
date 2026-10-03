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

        // Step D: Generate sequential token with daily reset (e.g. CARD-1003-001)
        var nextNumber = await GetNextTokenNumberAsync(doctor.DepartmentId);
        var dateStr = DateTime.UtcNow.ToString("MMdd");
        var tokenNumber = $"{doctor.Department.Code.Trim().ToUpper()}-{dateStr}-{nextNumber:D3}";

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

        var response = MapToResponse(token, token.Patient, token.Doctor);

        // Calculate live position and ETA countdown for active waiting patients
        if (token.Status == "Waiting")
        {
            var aheadCount = await _context.QueueTokens
                .AsNoTracking()
                .CountAsync(t => t.DoctorId == token.DoctorId
                              && t.BookedAtUtc >= today
                              && (t.Status == "Waiting" || t.Status == "Called")
                              && (t.PriorityScore > token.PriorityScore ||
                                 (t.PriorityScore == token.PriorityScore && t.BookedAtUtc < token.BookedAtUtc)));

            var avgConsult = token.Doctor?.AvgConsultationMinutes ?? 10;
            response.PatientsAhead = aheadCount;
            response.EstimatedWaitMinutes = aheadCount * avgConsult;
        }
        else if (token.Status == "Called")
        {
            response.PatientsAhead = 0;
            response.EstimatedWaitMinutes = 0;
        }

        return response;
    }

    // =========================================================================
    // 5. GET ALL ACTIVE TOKENS TODAY FOR USER & FAMILY
    // =========================================================================
    public async Task<IEnumerable<QueueTokenResponse>> GetUserActiveTokensAsync(Guid currentUserId)
    {
        var today = DateTime.UtcNow.Date;

        // 1. Get logged-in user's phone number
        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null) return Enumerable.Empty<QueueTokenResponse>();

        // 2. Find all patient IDs sharing this phone number (self + dependents)
        var familyPatientIds = await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber == user.PhoneNumber)
            .Select(p => p.Id)
            .ToListAsync();

        if (!familyPatientIds.Any()) return Enumerable.Empty<QueueTokenResponse>();

        // 3. Fetch all active tokens today
        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .Where(t => familyPatientIds.Contains(t.PatientId)
                     && t.BookedAtUtc >= today
                     && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"))
            .OrderBy(t => t.BookedAtUtc)
            .ToListAsync();

        var responseList = new List<QueueTokenResponse>();
        foreach (var token in tokens)
        {
            var res = MapToResponse(token, token.Patient!, token.Doctor!);

            if (token.Status == "Waiting")
            {
                res.PatientsAhead = await _context.QueueTokens
                    .AsNoTracking()
                    .CountAsync(t => t.DoctorId == token.DoctorId
                                  && t.BookedAtUtc >= today
                                  && (t.Status == "Waiting" || t.Status == "Called")
                                  && (t.PriorityScore > token.PriorityScore ||
                                     (t.PriorityScore == token.PriorityScore && t.BookedAtUtc < token.BookedAtUtc)));

                var avgMinutes = token.Doctor?.AvgConsultationMinutes ?? 10;
                res.EstimatedWaitMinutes = res.PatientsAhead * avgMinutes;
            }
            else if (token.Status == "Called")
            {
                res.PatientsAhead = 0;
                res.EstimatedWaitMinutes = 0;
            }

            responseList.Add(res);
        }

        return responseList;
    }

    // =========================================================================
    // 6. CANCEL ACTIVE BOOKING (Patient or Admin)
    // =========================================================================
    public async Task<QueueTokenResponse> CancelTokenAsync(Guid tokenId, Guid currentUserId, string? reason = null)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.Department)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");
        }

        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        bool isOwner = (token.Patient.UserId == currentUserId) 
                    || (token.Patient.PhoneNumber == user.PhoneNumber) 
                    || (user.Role == "Admin")
                    || await _context.Patients.AnyAsync(p => p.Id == token.PatientId && (p.UserId == currentUserId || p.PhoneNumber == user.PhoneNumber));
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("You are not authorized to cancel this token.");
        }

        if (token.Status == "Completed")
        {
            throw new InvalidOperationException("Cannot cancel a consultation that has already been completed.");
        }

        if (token.Status == "Cancelled")
        {
            throw new InvalidOperationException("This token has already been cancelled.");
        }

        var previousStatus = token.Status;
        token.Status = "Cancelled";

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = tokenId,
            PreviousStatus = previousStatus,
            NewStatus = "Cancelled",
            ChangeSource = user.Role == "Admin" ? "Admin" : "Patient",
            ChangedByUserId = currentUserId,
            Notes = string.IsNullOrWhiteSpace(reason) ? "Cancelled by patient" : $"Cancelled: {reason.Trim()}",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();

        return MapToResponse(token, token.Patient, token.Doctor);
    }

    // =========================================================================
    // 7. TRACK TOKEN (Public anonymous tracker for paper slip holders)
    // =========================================================================
    public async Task<QueueTokenResponse?> TrackTokenAsync(string tokenNumber)
    {
        var today = DateTime.UtcNow.Date;
        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.Department)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.TokenNumber == tokenNumber && t.BookedAtUtc >= today);

        if (token == null)
        {
            return null;
        }

        var res = MapToResponse(token, token.Patient!, token.Doctor!);

        // Compute dynamic queue wait metrics
        if (token.Status == "Waiting")
        {
            res.PatientsAhead = await _context.QueueTokens
                .AsNoTracking()
                .CountAsync(t => t.DoctorId == token.DoctorId
                              && t.BookedAtUtc >= today
                              && (t.Status == "Waiting" || t.Status == "Called")
                              && (t.PriorityScore > token.PriorityScore ||
                                 (t.PriorityScore == token.PriorityScore && t.BookedAtUtc < token.BookedAtUtc)));

            var avgMinutes = token.Doctor?.AvgConsultationMinutes ?? 10;
            res.EstimatedWaitMinutes = res.PatientsAhead * avgMinutes;
        }
        else if (token.Status == "Called")
        {
            res.PatientsAhead = 0;
            res.EstimatedWaitMinutes = 0;
        }

        return res;
    }

    // =========================================================================
    // HELPER: ATOMIC TOKEN COUNTER (Simple & Fast)
    // =========================================================================
    private async Task<int> GetNextTokenNumberAsync(Guid departmentId)
    {
        await using var cmd = _context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = @"
            UPDATE Departments 
            SET LastTokenNumber = CASE 
                    WHEN LastTokenDate = CAST(GETUTCDATE() AS DATE) THEN LastTokenNumber + 1 
                    ELSE 1 
                END,
                LastTokenDate = CAST(GETUTCDATE() AS DATE)
            OUTPUT INSERTED.LastTokenNumber 
            WHERE Id = @deptId";

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
