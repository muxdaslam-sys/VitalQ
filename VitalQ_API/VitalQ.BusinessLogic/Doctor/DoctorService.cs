using Microsoft.EntityFrameworkCore;
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

    // =========================================================================
    // 1. GET DOCTOR PROFILE BY USER ID
    // =========================================================================
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
            DepartmentId = doctor.DepartmentId,
            DepartmentName = doctor.Department?.Name ?? string.Empty,
            Specialization = doctor.Specialization,
            RoomNumber = doctor.RoomNumber,
            Status = doctor.Status,
            AvgConsultationMinutes = doctor.AvgConsultationMinutes,
            CreatedAtUtc = doctor.CreatedAtUtc
        };
    }

    // =========================================================================
    // 2. GET DOCTOR'S LIVE QUEUE
    // =========================================================================
    public async Task<IEnumerable<QueueTokenResponse>> GetMyQueueAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
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

    // =========================================================================
    // 3. GET PATIENT CURRENTLY INSIDE THE ROOM
    // =========================================================================
    public async Task<QueueTokenResponse?> GetCurrentCalledPatientAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DoctorId == doctorId &&
                        t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc &&
                        t.Status == "Called")
            .OrderByDescending(t => t.CalledAtUtc)
            .FirstOrDefaultAsync();

        return token == null ? null : MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 4. CALL NEXT PATIENT (Optimistic Concurrency via RowVersion)
    // =========================================================================
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

            await _context.TokenAuditLogs.AddAsync(new TokenAuditLog
            {
                Id = Guid.NewGuid(),
                QueueTokenId = active.Id,
                PreviousStatus = "Called",
                NewStatus = "Completed",
                ChangeSource = "Doctor",
                ChangedByUserId = doctor.UserId,
                Notes = "Automatically completed upon calling next patient.",
                ChangedAtUtc = DateTime.UtcNow
            });
        }

        // Pick top-priority waiting patient
        var nextToken = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
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

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = nextToken.Id,
            PreviousStatus = "Waiting",
            NewStatus = "Called",
            ChangeSource = "Doctor",
            ChangedByUserId = doctor.UserId,
            Notes = $"Called into room {doctor.RoomNumber} by Dr. {doctor.User.FullName}",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();
        return MapToResponse(nextToken, 0, 0);
    }

    // =========================================================================
    // 5. SKIP PATIENT (Patient did not enter room)
    // =========================================================================
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

        var previousStatus = token.Status;
        token.Status = "Skipped";

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = tokenId,
            PreviousStatus = previousStatus,
            NewStatus = "Skipped",
            ChangeSource = "Doctor",
            ChangedByUserId = doctorUserId,
            Notes = "Patient did not respond / skipped by doctor.",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 6. REQUEUE PATIENT (Patient arrived after being skipped)
    // =========================================================================
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

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = tokenId,
            PreviousStatus = "Skipped",
            NewStatus = "Waiting",
            ChangeSource = "Doctor",
            ChangedByUserId = doctorUserId,
            Notes = "Patient returned; reinstated to waiting queue.",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 7. COMPLETE CONSULTATION
    // =========================================================================
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

        var previousStatus = token.Status;
        token.Status = "Completed";
        token.CompletedAtUtc = DateTime.UtcNow;
        token.ConsultationNotes = request.ConsultationNotes?.Trim();

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = tokenId,
            PreviousStatus = previousStatus,
            NewStatus = "Completed",
            ChangeSource = "Doctor",
            ChangedByUserId = doctorUserId,
            Notes = string.IsNullOrWhiteSpace(request.ConsultationNotes)
                ? "Consultation completed"
                : $"Consultation completed. Notes: {request.ConsultationNotes.Trim()}",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 8. RECALCULATE QUEUE SCORES (Aging engine loop)
    // =========================================================================
    public async Task RecalculateQueueScoresAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var waitingTokens = await _context.QueueTokens
            .Where(t => t.Status == "Waiting" && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.TriagedAtUtc.HasValue)
            .ToListAsync();

        if (!waitingTokens.Any()) return;

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
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }
    }

    private static QueueTokenResponse MapToResponse(QueueToken t, int patientsAhead, int avgConsultationMins) => new()
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
        DoctorName = t.Doctor?.User?.FullName ?? string.Empty,
        RoomNumber = t.Doctor?.RoomNumber ?? string.Empty,
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
        RowVersion = t.RowVersion ?? Array.Empty<byte>()
    };
}
