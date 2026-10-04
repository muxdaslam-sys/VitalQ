using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Handles public department lookups, doctor schedules, and paper slip tracking.
/// Uses in-memory caching to eliminate redundant database hits during peak hours.
/// </summary>
public class PublicService : IPublicService
{
    private readonly VitalQDbContext _context;
    private readonly IMemoryCache _cache;

    public PublicService(VitalQDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Returns active clinical departments. Cached for 2 minutes.
    /// </summary>
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

    /// <summary>
    /// Returns on-duty doctors in a department. Cached for 1 minute.
    /// </summary>
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

    /// <summary>
    /// Public slip tracker: Allows patients holding a paper slip to view their live status on Mobile or PC.
    /// Computes accurate patients ahead and estimated wait minutes.
    /// </summary>
    public async Task<QueueTokenResponse?> TrackTokenAsync(string tokenNumber)
    {
        if (string.IsNullOrWhiteSpace(tokenNumber)) return null;

        var today = DateTime.UtcNow.Date;
        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.TokenNumber == tokenNumber.Trim() && t.BookedAtUtc >= today);

        if (token == null) return null;

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
