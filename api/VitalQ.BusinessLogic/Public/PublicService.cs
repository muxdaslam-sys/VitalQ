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
                    PhoneNumber = d.User.PhoneNumber,
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
        var tomorrow = today.AddDays(1);
        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment).ThenInclude(ta => ta!.NurseUser)
            .Include(t => t.TriageAssessment).ThenInclude(ta => ta!.NursingStation)
            .FirstOrDefaultAsync(t => t.TokenNumber == tokenNumber.Trim() && t.BookedAtUtc >= today && t.BookedAtUtc < tomorrow);

        if (token == null) return null;

        var res = MapToResponse(token, token.Patient!, token.Doctor!);

        // Compute dynamic queue wait metrics
        if (token.Status == "Waiting")
        {
            res.PatientsAhead = await _context.QueueTokens
                .AsNoTracking()
                .CountAsync(t => t.DoctorId == token.DoctorId
                              && t.BookedAtUtc >= today
                              && t.BookedAtUtc < tomorrow
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

    private static QueueTokenResponse MapToResponse(QueueToken t, Patient p, Doctor d)
    {
        var station = t.TriageAssessment?.NursingStation 
            ?? d.Department?.NursingStations?.FirstOrDefault(s => s.IsActive)
            ?? t.Department?.NursingStations?.FirstOrDefault(s => s.IsActive);

        var stationName = station?.StationName ?? "Nursing Triage Desk";
        var stationFloor = station?.LocationFloor ?? d.Department?.LocationFloor ?? t.Department?.LocationFloor ?? "Floor 1";
        var room = d.RoomNumber ?? string.Empty;
        var doctorName = d.User?.FullName ?? string.Empty;

        var nextStepInstruction = t.Status switch
        {
            "Booked" => $"Please report to {stationName} ({stationFloor}) for Vital Signs Intake.",
            "Waiting" => $"Vitals recorded ({t.TriageAssessment?.TriageLevel ?? "Green"}). Please wait in the lounge outside Room {room} ({stationFloor}).",
            "Called" => $"Please enter Room {room} for your consultation with Dr. {doctorName}.",
            "Completed" => "Consultation completed. Thank you for visiting VitalQ.",
            "Cancelled" => "Appointment was cancelled.",
            "Skipped" => $"Missed call. Please report to {stationName} ({stationFloor}) or Room {room} immediately to retain your place.",
            "Expired" => "Consultation slip has expired.",
            _ => $"Please proceed to {stationFloor}."
        };

        int skipCount = 0;
        if (!string.IsNullOrEmpty(t.ConsultationNotes))
        {
            if (t.ConsultationNotes.Contains("Call 3 of 3") || t.ConsultationNotes.Contains("Absent after 3 calls")) skipCount = 3;
            else if (t.ConsultationNotes.Contains("Call 2 of 3")) skipCount = 2;
            else if (t.ConsultationNotes.Contains("Call 1 of 3")) skipCount = 1;
        }

        return new QueueTokenResponse
        {
            Id = t.Id,
            TokenNumber = t.TokenNumber,
            PatientId = p.Id,
            PatientName = p.FullName,
            PatientPhone = p.PhoneNumber,
            DepartmentId = d.DepartmentId,
            DepartmentName = d.Department?.Name ?? t.Department?.Name ?? string.Empty,
            DepartmentCode = d.Department?.Code ?? t.Department?.Code ?? string.Empty,
            DoctorId = d.Id,
            DoctorName = doctorName,
            RoomNumber = room,
            Status = t.Status,
            PriorityScore = t.PriorityScore,
            BaseWeight = t.BaseWeight,
            TriageLevel = t.TriageAssessment?.TriageLevel,
            SpO2 = t.TriageAssessment?.SpO2,
            SystolicBp = t.TriageAssessment?.SystolicBp,
            DiastolicBp = t.TriageAssessment?.DiastolicBp,
            HeartRate = t.TriageAssessment?.HeartRate,
            Temperature = t.TriageAssessment?.Temperature,
            PainScale = t.TriageAssessment?.PainScale,
            NurseNotes = t.TriageAssessment?.NurseNotes,
            NurseName = t.TriageAssessment?.NurseUser?.FullName,
            SkipCount = skipCount,
            NursingStationId = station?.Id,
            NursingStationName = stationName,
            LocationFloor = stationFloor,
            NextStepInstruction = nextStepInstruction,
            BookedAtUtc = t.BookedAtUtc,
            TriagedAtUtc = t.TriagedAtUtc,
            CalledAtUtc = t.CalledAtUtc,
            CompletedAtUtc = t.CompletedAtUtc,
            ConsultationNotes = t.ConsultationNotes,
            RowVersion = t.RowVersion ?? Array.Empty<byte>()
        };
    }
}
