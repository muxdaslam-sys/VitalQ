using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Common;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Service powering the Doctor Consultation Room console on PC.
/// Handles priority queue viewing, calling next, skipping, requeueing, and consultation completion.
/// </summary>
public class DoctorService : IDoctorService
{
    private readonly VitalQDbContext _context;

    public DoctorService(VitalQDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves the doctor profile linked to the specified user ID.
    /// </summary>
    public async Task<DoctorResponse?> GetDoctorByUserIdAsync(Guid userId)
    {
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == userId);

        if (doctor == null) return null;

        return new DoctorResponse
        {
            Id = doctor.Id,
            UserId = doctor.UserId,
            DoctorName = doctor.User?.FullName ?? string.Empty,
            Username = doctor.User?.Username ?? string.Empty,
            PhoneNumber = doctor.User?.PhoneNumber ?? string.Empty,
            DepartmentId = doctor.DepartmentId,
            DepartmentName = doctor.Department?.Name ?? string.Empty,
            Specialization = doctor.Specialization,
            RoomNumber = doctor.RoomNumber,
            Status = doctor.Status,
            AvgConsultationMinutes = doctor.AvgConsultationMinutes,
            CreatedAtUtc = doctor.CreatedAtUtc
        };
    }

    /// <summary>
    /// Retrieves the live consultation queue for a doctor, partitioned into called, waiting, and skipped.
    /// </summary>
    public async Task<IEnumerable<QueueTokenResponse>> GetMyQueueAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department).ThenInclude(d => d.NursingStations)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Where(t => t.DoctorId == doctorId &&
                        t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc &&
                        (t.Status == "Called" || t.Status == "Waiting" || t.Status == "Skipped"))
            .ToListAsync();

        // 1. Called patient (currently in room)
        var called = tokens.Where(t => t.Status == "Called").OrderBy(t => t.CalledAtUtc).ToList();

        // 2. Waiting patients ranked by PriorityScore DESC, BookedAtUtc ASC
        var waiting = tokens.Where(t => t.Status == "Waiting")
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToList();

        // 3. Skipped patients on hold
        var skipped = tokens.Where(t => t.Status == "Skipped").OrderBy(t => t.BookedAtUtc).ToList();

        var result = new List<QueueTokenResponse>();

        foreach (var c in called)
        {
            result.Add(MapToResponse(c, 0, 0));
        }

        for (int i = 0; i < waiting.Count; i++)
        {
            var w = waiting[i];
            var avgMins = w.Doctor?.AvgConsultationMinutes ?? 10;
            result.Add(MapToResponse(w, i, avgMins));
        }

        foreach (var s in skipped)
        {
            result.Add(MapToResponse(s, 0, 0));
        }

        return result;
    }

    /// <summary>
    /// Retrieves the patient currently called into the doctor's consultation room.
    /// </summary>
    public async Task<QueueTokenResponse?> GetCurrentCalledPatientAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department).ThenInclude(d => d.NursingStations)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Where(t => t.DoctorId == doctorId &&
                        t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc &&
                        t.Status == "Called")
            .OrderByDescending(t => t.CalledAtUtc)
            .FirstOrDefaultAsync();

        return token == null ? null : MapToResponse(token, 0, 0);
    }

    /// <summary>
    /// Calls the next highest-priority waiting patient into the consultation room.
    /// </summary>
    public async Task<QueueTokenResponse?> CallNextPatientAsync(Guid doctorId)
    {
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null)
        {
            throw new InvalidOperationException("Doctor record not found.");
        }

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        // Automatically complete or skip any existing 'Called' patient before calling next
        var currentlyCalled = await _context.QueueTokens
            .Where(t => t.DoctorId == doctorId && t.Status == "Called" && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc)
            .ToListAsync();

        foreach (var active in currentlyCalled)
        {
            active.Status = "Completed";
            active.CompletedAtUtc = DateTime.UtcNow;
        }

        // Pick top-priority waiting patient
        var nextToken = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department).ThenInclude(d => d.NursingStations)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Where(t => t.DoctorId == doctorId &&
                        t.Status == "Waiting" &&
                        t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc)
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .FirstOrDefaultAsync();

        if (nextToken == null)
        {
            await _context.SaveChangesAsync();
            return null;
        }

        nextToken.Status = "Called";
        nextToken.CalledAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToResponse(nextToken, 0, 0);
    }

    /// <summary>
    /// Marks a called patient as skipped if they do not enter the room.
    /// </summary>
    public async Task<QueueTokenResponse> SkipPatientAsync(Guid tokenId, Guid doctorUserId)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");
        }

        if (token.Status != "Called" && token.Status != "Waiting")
        {
            throw new InvalidOperationException($"Cannot skip a patient with status '{token.Status}'.");
        }

        token.Status = "Skipped";

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    /// <summary>
    /// Requeues a previously skipped patient back into the waiting line.
    /// </summary>
    public async Task<QueueTokenResponse> RequeuePatientAsync(Guid tokenId, Guid doctorUserId)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");
        }

        if (token.Status != "Skipped")
        {
            throw new InvalidOperationException($"Cannot requeue a patient with status '{token.Status}'. Must be 'Skipped'.");
        }

        token.Status = "Waiting";

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    /// <summary>
    /// Completes consultation and logs clinical notes and diagnosis.
    /// </summary>
    public async Task<QueueTokenResponse> CompleteConsultationAsync(Guid tokenId, Guid doctorUserId, CompleteConsultationRequest request)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");
        }

        if (token.Status != "Called" && token.Status != "Waiting")
        {
            throw new InvalidOperationException($"Cannot complete consultation for token with status '{token.Status}'.");
        }

        token.Status = "Completed";
        token.CompletedAtUtc = DateTime.UtcNow;
        token.ConsultationNotes = request.ConsultationNotes?.Trim();

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    /// <summary>
    /// Recalculates dynamic priority scores and applies 60-minute anti-starvation SLA auto-escalation.
    /// </summary>
    public async Task<bool> RecalculateQueueScoresAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var waitingTokens = await _context.QueueTokens
            .Where(t => t.Status == "Waiting" && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.TriagedAtUtc.HasValue)
            .ToListAsync();

        if (!waitingTokens.Any()) return false;

        var now = DateTime.UtcNow;
        bool hasChanges = false;

        foreach (var token in waitingTokens)
        {
            var newScore = PriorityScoreCalculator.CalculateScore(token.BaseWeight, token.TriagedAtUtc!.Value, now);
            if (newScore != token.PriorityScore)
            {
                token.PriorityScore = newScore;
                hasChanges = true;
            }

            // 60-Minute Anti-Starvation SLA Auto-Escalation:
            // If a Green patient has waited >= 60 minutes, elevate their base weight to Yellow (500)
            if (token.BaseWeight == PriorityScoreCalculator.BaseWeightGreen &&
                PriorityScoreCalculator.IsEscalated(token.BaseWeight, token.TriagedAtUtc!.Value, now))
            {
                token.BaseWeight = PriorityScoreCalculator.BaseWeightYellow; // Promoted to 500
                if (string.IsNullOrEmpty(token.ConsultationNotes) || !token.ConsultationNotes.Contains("SLA Escalated"))
                {
                    token.ConsultationNotes = string.IsNullOrEmpty(token.ConsultationNotes)
                        ? "[⏱️ 60m+ Wait: Auto-Escalated to Urgent]"
                        : $"{token.ConsultationNotes} [⏱️ 60m+ Wait: Auto-Escalated to Urgent]";
                }
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }

        return hasChanges;
    }

    /// <summary>
    /// Sweeps and marks unvisited consultation tokens from prior dates as expired.
    /// </summary>
    public async Task<int> ExpireStaleTokensAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;

        return await _context.QueueTokens
            .Where(t => t.BookedAtUtc < todayUtc && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called" || t.Status == "Skipped"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "Expired")
                .SetProperty(t => t.CompletedAtUtc, t => t.BookedAtUtc)
                .SetProperty(t => t.ConsultationNotes, "Expired — Patient did not attend on scheduled date."));
    }

    private static QueueTokenResponse MapToResponse(QueueToken t, int patientsAhead, int avgConsultationMins)
    {
        var station = t.TriageAssessment?.NursingStation 
            ?? t.Department?.NursingStations?.FirstOrDefault(s => s.IsActive)
            ?? t.Doctor?.Department?.NursingStations?.FirstOrDefault(s => s.IsActive);

        var stationName = station?.StationName ?? "Nursing Triage Desk";
        var stationFloor = station?.LocationFloor ?? t.Department?.LocationFloor ?? "Floor 1";
        var room = t.Doctor?.RoomNumber ?? "Consultation Room";
        var doctorName = t.Doctor?.User?.FullName ?? "Doctor";

        var nextStepInstruction = t.Status switch
        {
            "Booked" => $"Please report to {stationName} ({stationFloor}) for Vital Signs Intake.",
            "Waiting" => $"Vitals recorded ({t.TriageAssessment?.TriageLevel ?? "Green"}). Please wait in the lounge outside Room {room} ({stationFloor}).",
            "Called" => $"Please enter Room {room} for your consultation with Dr. {doctorName}.",
            "Completed" => "Consultation completed. Thank you for visiting VitalQ.",
            "Cancelled" => "Appointment was cancelled.",
            _ => $"Please proceed to {stationFloor}."
        };

        return new QueueTokenResponse
        {
            Id = t.Id,
            TokenNumber = t.TokenNumber,
            PatientId = t.PatientId,
            PatientName = t.Patient?.FullName ?? string.Empty,
            PatientPhone = t.Patient?.PhoneNumber ?? string.Empty,
            DepartmentId = t.DepartmentId,
            DepartmentName = t.Department?.Name ?? string.Empty,
            DepartmentCode = t.Department?.Code ?? string.Empty,
            DoctorId = t.DoctorId,
            DoctorName = doctorName,
            RoomNumber = room,
            Status = t.Status,
            PriorityScore = t.PriorityScore,
            BaseWeight = t.BaseWeight,
            TriageLevel = t.TriageAssessment?.TriageLevel,
            PatientsAhead = patientsAhead,
            EstimatedWaitMinutes = patientsAhead * avgConsultationMins,
            BookedAtUtc = t.BookedAtUtc,
            TriagedAtUtc = t.TriagedAtUtc,
            CalledAtUtc = t.CalledAtUtc,
            CompletedAtUtc = t.CompletedAtUtc,
            ConsultationNotes = t.ConsultationNotes,
            RowVersion = t.RowVersion ?? Array.Empty<byte>(),
            NursingStationId = station?.Id,
            NursingStationName = stationName,
            LocationFloor = stationFloor,
            NextStepInstruction = nextStepInstruction
        };
    }
}
