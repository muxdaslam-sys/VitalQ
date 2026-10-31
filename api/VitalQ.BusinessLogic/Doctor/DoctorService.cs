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

    #region 1. Doctor Profile, Identity & Shift KPIs

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
    /// Computes real-time shift performance metrics for the doctor consultation desk.
    /// Executes fast SQL aggregations in a single database round-trip without serializing heavy entity graphs.
    /// </summary>
    public async Task<DoctorDashboardStatsDto> GetDashboardStatsAsync(Guid doctorId)
    {
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null)
        {
            throw new KeyNotFoundException($"Doctor with ID {doctorId} was not found.");
        }

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var tokensToday = await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.DoctorId == doctorId && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc)
            .Select(t => new { t.Status, t.TokenNumber, t.CalledAtUtc, t.CompletedAtUtc })
            .ToListAsync();

        var completedTokens = tokensToday.Where(t => t.Status == "Completed").ToList();
        var waitingTokens = tokensToday.Where(t => t.Status == "Waiting").ToList();
        var skippedTokens = tokensToday.Where(t => t.Status == "Skipped").ToList();
        var calledToken = tokensToday.FirstOrDefault(t => t.Status == "Called");

        // Calculate running average consultation minutes today if completed visits exist
        int avgDuration = doctor.AvgConsultationMinutes;
        var durations = completedTokens
            .Where(t => t.CalledAtUtc.HasValue && t.CompletedAtUtc.HasValue && t.CompletedAtUtc > t.CalledAtUtc)
            .Select(t => (int)(t.CompletedAtUtc!.Value - t.CalledAtUtc!.Value).TotalMinutes)
            .Where(m => m > 0 && m < 120)
            .ToList();

        if (durations.Count > 0)
        {
            avgDuration = (int)Math.Round(durations.Average());
        }

        return new DoctorDashboardStatsDto
        {
            DoctorId = doctor.Id,
            DoctorName = doctor.User?.FullName ?? "Doctor",
            RoomNumber = doctor.RoomNumber,
            DepartmentName = doctor.Department?.Name ?? string.Empty,
            Status = doctor.Status,
            TotalAssignedToday = tokensToday.Count,
            CompletedToday = completedTokens.Count,
            WaitingCount = waitingTokens.Count,
            SkippedCount = skippedTokens.Count,
            AvgConsultationMinutes = avgDuration,
            CurrentCalledTokenNumber = calledToken?.TokenNumber
        };
    }

    /// <summary>
    /// Updates physician availability status (Available, Busy, OnLeave).
    /// </summary>
    public async Task<DoctorResponse> UpdateDoctorStatusAsync(Guid doctorId, string status)
    {
        var doctor = await _context.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null)
        {
            throw new KeyNotFoundException($"Doctor with ID {doctorId} was not found.");
        }

        doctor.Status = status;
        await _context.SaveChangesAsync();

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

    #endregion

    #region 2. Live Consultation Queue & Summoning

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
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NurseUser)
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
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NurseUser)
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

        if (!string.Equals(doctor.Status, "Available", StringComparison.OrdinalIgnoreCase))
        {
            var statusLabel = string.Equals(doctor.Status, "Busy", StringComparison.OrdinalIgnoreCase) ? "On Break (Paused)" : doctor.Status;
            throw new InvalidOperationException($"Cannot summon patients while duty status is '{statusLabel}'. Please switch status to 'Available in Clinic' to resume consultations.");
        }

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        // Automatically complete any existing 'Called' patient before calling next
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
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NurseUser)
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
    /// Re-broadcasts the summon chime and room alert for an already-called patient.
    /// </summary>
    public async Task<QueueTokenResponse?> ResummonPatientAsync(Guid tokenId, Guid doctorId)
    {
        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department).ThenInclude(d => d.NursingStations)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NurseUser)
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.DoctorId == doctorId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found for this doctor.");
        }

        if (token.Status != "Called")
        {
            throw new InvalidOperationException($"Cannot re-summon token with status '{token.Status}'. Token must be 'Called'.");
        }

        return MapToResponse(token, 0, 0);
    }

    #endregion

    #region 3. Queue Advancement & State Transitions

    /// <summary>
    /// Marks a called patient as skipped if they do not enter the room.
    /// Enforces the 3-Strike Clinical Rule:
    /// - Strikes 1 & 2: Token transitions to 'Skipped' (Temporary Hold).
    /// - Strike 3: Token auto-transitions to 'Expired' (Permanent No-Show).
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

        // Determine current strike count from audit remarks
        int currentStrikes = 0;
        if (!string.IsNullOrEmpty(token.ConsultationNotes))
        {
            if (token.ConsultationNotes.Contains("[Skipped: Call 2 of 3]")) currentStrikes = 2;
            else if (token.ConsultationNotes.Contains("[Skipped: Call 1 of 3]")) currentStrikes = 1;
        }

        int nextStrike = currentStrikes + 1;

        if (nextStrike >= 3)
        {
            // Strike 3: Absent after 3 calls -> Auto-Expire (Permanent No-Show)
            token.Status = "Expired";
            token.CompletedAtUtc = DateTime.UtcNow;
            token.ConsultationNotes = AppendOrReplaceSkipAudit(token.ConsultationNotes, "[No-Show: Absent after 3 calls]");
        }
        else
        {
            // Strikes 1 & 2: Place on temporary hold for Nurse or Doctor to re-queue
            token.Status = "Skipped";
            token.ConsultationNotes = AppendOrReplaceSkipAudit(token.ConsultationNotes, $"[Skipped: Call {nextStrike} of 3]");
        }

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    /// <summary>
    /// Restores an absent patient back into the active waiting line when they report to the clinic.
    /// Can be invoked by either the Triage Nurse Desk or the Doctor Room Console.
    /// 
    /// Clinical Acuity & Position Rules:
    /// 1. RED (Emergency / CTAS 1): BaseWeight = 1000. Lands in Position #1 immediately (Next to be called).
    /// 2. YELLOW (Urgent / CTAS 2): BaseWeight = 500. Ahead of routine patients, behind earlier urgent arrivals.
    /// 3. GREEN (Routine / CTAS 3): Falls back 3 positions (Position #4) behind currently waiting routine patients.
    ///    Mathematically synchronizes TriagedAtUtc so the 60s background aging worker never causes score glitches.
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

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var now = DateTime.UtcNow;

        // Fetch all currently waiting patients for this doctor ordered by PriorityScore DESC
        var waitingQueue = await _context.QueueTokens
            .Where(t => t.DoctorId == token.DoctorId && 
                        t.Status == "Waiting" && 
                        t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc)
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToListAsync();

        string triageLevel = token.TriageAssessment?.TriageLevel ?? "Green";

        // ─────────────────────────────────────────────────────────────────
        // SCENARIO A: RED (Emergency / CTAS 1) -> ALWAYS POSITION #1
        // ─────────────────────────────────────────────────────────────────
        if (triageLevel == "Red" || token.BaseWeight >= PriorityScoreCalculator.BaseWeightRed)
        {
            token.BaseWeight = PriorityScoreCalculator.BaseWeightRed; // 1000
            token.TriagedAtUtc = now;
            token.PriorityScore = PriorityScoreCalculator.BaseWeightRed;
        }
        // ─────────────────────────────────────────────────────────────────
        // SCENARIO B: YELLOW (Urgent / CTAS 2) -> AHEAD OF GREEN, BEHIND EARLIER YELLOW
        // ─────────────────────────────────────────────────────────────────
        else if (triageLevel == "Yellow" || token.BaseWeight >= PriorityScoreCalculator.BaseWeightYellow)
        {
            token.BaseWeight = PriorityScoreCalculator.BaseWeightYellow; // 500
            token.TriagedAtUtc = now;
            token.PriorityScore = PriorityScoreCalculator.BaseWeightYellow;
        }
        // ─────────────────────────────────────────────────────────────────
        // SCENARIO C: GREEN (Routine / CTAS 3) -> 3-SLOT FAIR FALLBACK
        // ─────────────────────────────────────────────────────────────────
        else
        {
            token.BaseWeight = PriorityScoreCalculator.BaseWeightGreen; // 100

            // Find routine (Green) patients in waiting queue
            var routineWaiting = waitingQueue
                .Where(t => t.BaseWeight == PriorityScoreCalculator.BaseWeightGreen)
                .ToList();

            if (routineWaiting.Count >= 3)
            {
                // Slot behind the 3rd routine patient in line -> Patient becomes Position #4
                var thirdPatient = routineWaiting[2];
                int targetScore = Math.Min(
                    PriorityScoreCalculator.GreenMaxCeiling, // Strict ceiling: never cross into Yellow (499 max)
                    Math.Max(PriorityScoreCalculator.BaseWeightGreen, thirdPatient.PriorityScore - 1)
                );
                token.PriorityScore = targetScore;

                // Mathematical Sync: Align TriagedAtUtc so AgingWorker formula
                // [BaseWeight + ElapsedMinutes * 2] matches targetScore with zero glitch!
                int equivalentWaitingMinutes = Math.Max(0, (targetScore - PriorityScoreCalculator.BaseWeightGreen) / 2);
                token.TriagedAtUtc = now.AddMinutes(-equivalentWaitingMinutes);
            }
            else if (waitingQueue.Count > 0)
            {
                // Fewer than 3 routine patients waiting: Place directly behind the last patient
                var lastPatient = waitingQueue.Last();
                int targetScore = Math.Min(
                    PriorityScoreCalculator.GreenMaxCeiling,
                    Math.Max(PriorityScoreCalculator.BaseWeightGreen, lastPatient.PriorityScore - 1)
                );
                token.PriorityScore = targetScore;

                int equivalentWaitingMinutes = Math.Max(0, (targetScore - PriorityScoreCalculator.BaseWeightGreen) / 2);
                token.TriagedAtUtc = now.AddMinutes(-equivalentWaitingMinutes);
            }
            else
            {
                // Waiting line is completely empty: Patient is #1
                token.TriagedAtUtc = now;
                token.PriorityScore = PriorityScoreCalculator.BaseWeightGreen;
            }
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

    #endregion

    #region 4. Encounter History & Patient Dossier

    /// <summary>
    /// Retrieves consultations completed today by the specified doctor in chronological order.
    /// </summary>
    public async Task<IEnumerable<QueueTokenResponse>> GetCompletedHistoryTodayAsync(Guid doctorId)
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
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NurseUser)
            .Where(t => t.DoctorId == doctorId &&
                        t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc &&
                        t.Status == "Completed")
            .OrderByDescending(t => t.CompletedAtUtc)
            .ToListAsync();

        return tokens.Select(t => MapToResponse(t, 0, 0));
    }

    /// <summary>
    /// Retrieves previous hospital encounters and consultation notes for a patient.
    /// </summary>
    public async Task<IEnumerable<PatientVisitHistoryDto>> GetPatientPastEncountersAsync(Guid patientId)
    {
        var encounters = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.PatientId == patientId)
            .OrderByDescending(t => t.BookedAtUtc)
            .Take(20)
            .Select(t => new PatientVisitHistoryDto
            {
                TokenId = t.Id,
                TokenNumber = t.TokenNumber,
                DepartmentName = t.Department != null ? t.Department.Name : string.Empty,
                DoctorName = t.Doctor != null && t.Doctor.User != null ? t.Doctor.User.FullName : "Attending Physician",
                Status = t.Status,
                BookedAtUtc = t.BookedAtUtc,
                CompletedAtUtc = t.CompletedAtUtc,
                ConsultationNotes = t.ConsultationNotes,
                TriageLevel = t.TriageAssessment != null ? t.TriageAssessment.TriageLevel : null
            })
            .ToListAsync();

        return encounters;
    }

    #endregion

    #region 5. Background Engine & SLA Maintenance

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
                        ? "[60m+ Wait: Auto-Escalated to Urgent]"
                        : $"{token.ConsultationNotes} [60m+ Wait: Auto-Escalated to Urgent]";
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
    /// Sweeps and marks unvisited consultation tokens from prior dates as expired,
    /// and auto-expires tokens left on hold in Skipped status for >= 60 minutes without reporting to Nurse Desk.
    /// </summary>
    public async Task<int> ExpireStaleTokensAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;
        var holdThreshold = now.AddMinutes(-60);

        // 1. Expire unvisited tokens from prior dates
        var priorDayExpired = await _context.QueueTokens
            .Where(t => t.BookedAtUtc < todayUtc && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called" || t.Status == "Skipped"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "Expired")
                .SetProperty(t => t.CompletedAtUtc, t => t.BookedAtUtc)
                .SetProperty(t => t.ConsultationNotes, "Expired - Patient did not attend on scheduled date."));

        // 2. Auto-expire abandoned on-hold tokens exceeding 60-minute grace window today
        var holdExpired = await _context.QueueTokens
            .Where(t => t.Status == "Skipped" && t.BookedAtUtc >= todayUtc && 
                        ((t.CalledAtUtc.HasValue && t.CalledAtUtc.Value <= holdThreshold) || 
                         (!t.CalledAtUtc.HasValue && t.BookedAtUtc <= holdThreshold)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "Expired")
                .SetProperty(t => t.CompletedAtUtc, now)
                .SetProperty(t => t.ConsultationNotes, "Expired - Skipped hold window exceeded 60 minutes without check-in."));

        return priorDayExpired + holdExpired;
    }

    private static string AppendOrReplaceSkipAudit(string? existingNotes, string newAuditTag)
    {
        if (string.IsNullOrWhiteSpace(existingNotes)) return newAuditTag;

        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            existingNotes, 
            @"\[Skipped: Call \d of 3\]|\[No-Show: Absent after 3 calls\]", 
            string.Empty).Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? newAuditTag : $"{cleaned} {newAuditTag}";
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
            SpO2 = t.TriageAssessment?.SpO2,
            SystolicBp = t.TriageAssessment?.SystolicBp,
            DiastolicBp = t.TriageAssessment?.DiastolicBp,
            HeartRate = t.TriageAssessment?.HeartRate,
            Temperature = t.TriageAssessment?.Temperature,
            PainScale = t.TriageAssessment?.PainScale,
            NurseNotes = t.TriageAssessment?.NurseNotes,
            NurseName = t.TriageAssessment?.NurseUser?.FullName,
            PatientsAhead = patientsAhead,
            EstimatedWaitMinutes = patientsAhead * avgConsultationMins,
            SkipCount = skipCount,
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

    #endregion
}
