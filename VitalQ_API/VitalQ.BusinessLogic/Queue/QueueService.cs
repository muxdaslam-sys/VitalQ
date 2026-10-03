using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

public class QueueService : IQueueService
{
    private readonly VitalQDbContext _context;

    public QueueService(VitalQDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // 1. GET DOCTOR LIVE QUEUE (Sorted by PriorityScore DESC, BookedAtUtc ASC)
    // =========================================================================
    public async Task<IEnumerable<QueueTokenResponse>> GetDoctorQueueAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DoctorId == doctorId &&
                        t.BookedAtUtc.Date == todayUtc &&
                        (t.Status == "Called" || t.Status == "Waiting" || t.Status == "Skipped"))
            .ToListAsync();

        // 1. Called patients first (active in room)
        var called = tokens
            .Where(t => t.Status == "Called")
            .OrderBy(t => t.CalledAtUtc)
            .ToList();

        // 2. Waiting patients ranked by PriorityScore DESC, BookedAtUtc ASC
        var waiting = tokens
            .Where(t => t.Status == "Waiting")
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToList();

        // 3. Skipped / On-Hold patients
        var skipped = tokens
            .Where(t => t.Status == "Skipped")
            .OrderBy(t => t.BookedAtUtc)
            .ToList();

        var result = new List<QueueTokenResponse>();

        // Add called
        foreach (var c in called)
        {
            result.Add(MapToResponse(c, 0, 0));
        }

        // Add waiting with accurate live position and wait times
        for (int i = 0; i < waiting.Count; i++)
        {
            var w = waiting[i];
            var avgMins = w.Doctor?.AvgConsultationMinutes ?? 10;
            result.Add(MapToResponse(w, i, avgMins));
        }

        // Add skipped
        foreach (var s in skipped)
        {
            result.Add(MapToResponse(s, 0, 0));
        }

        return result;
    }

    // =========================================================================
    // 2. GET DOCTOR PROFILE BY LOGGED-IN USER ID
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
    // 3. GET CURRENTLY CALLED PATIENT FOR DOCTOR
    // =========================================================================
    public async Task<QueueTokenResponse?> GetCurrentCalledPatientAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DoctorId == doctorId && t.Status == "Called" && t.BookedAtUtc.Date == todayUtc)
            .OrderByDescending(t => t.CalledAtUtc)
            .FirstOrDefaultAsync();

        return token == null ? null : MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 4. CALL NEXT PATIENT (Guarded by RowVersion Optimistic Concurrency)
    // =========================================================================
    public async Task<QueueTokenResponse?> CallNextPatientAsync(Guid doctorId)
    {
        var todayUtc = DateTime.UtcNow.Date;

        // Clinical Safety Guard: Check if a consultation is already in progress
        var currentCalled = await _context.QueueTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.DoctorId == doctorId && t.Status == "Called" && t.BookedAtUtc.Date == todayUtc);

        if (currentCalled != null)
        {
            throw new InvalidOperationException($"Patient {currentCalled.TokenNumber} is currently in consultation. Please complete or skip them before calling the next patient.");
        }

        // Select the top-priority waiting patient
        var nextToken = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DoctorId == doctorId && t.Status == "Waiting" && t.BookedAtUtc.Date == todayUtc)
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .FirstOrDefaultAsync();

        if (nextToken == null)
        {
            return null;
        }

        // Move to Called
        var previousStatus = nextToken.Status;
        nextToken.Status = "Called";
        nextToken.CalledAtUtc = DateTime.UtcNow;

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = nextToken.Id,
            PreviousStatus = previousStatus,
            NewStatus = "Called",
            ChangeSource = "Doctor",
            ChangedByUserId = nextToken.Doctor?.UserId,
            Notes = $"Summoned into {(string.IsNullOrEmpty(nextToken.Doctor?.RoomNumber) ? "Consultation Room" : nextToken.Doctor.RoomNumber)}",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("This patient was modified or called by another session. Please refresh your queue.");
        }

        return MapToResponse(nextToken, 0, 0);
    }

    // =========================================================================
    // 5. SKIP PATIENT (Mark Absent / Place on Hold)
    // =========================================================================
    public async Task<QueueTokenResponse> SkipPatientAsync(Guid tokenId, Guid doctorUserId)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");
        }

        if (token.Status != "Called" && token.Status != "Waiting")
        {
            throw new InvalidOperationException($"Cannot skip a token with status '{token.Status}'.");
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
            Notes = "Patient marked absent / placed on hold",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 6. RE-QUEUE PATIENT (When Absent Patient Returns)
    // =========================================================================
    public async Task<QueueTokenResponse> RequeuePatientAsync(Guid tokenId, Guid doctorUserId)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");
        }

        if (token.Status != "Skipped")
        {
            throw new InvalidOperationException($"Only Skipped tokens can be re-queued. Current status is '{token.Status}'.");
        }

        var previousStatus = token.Status;
        token.Status = "Waiting";

        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = tokenId,
            PreviousStatus = previousStatus,
            NewStatus = "Waiting",
            ChangeSource = "Doctor",
            ChangedByUserId = doctorUserId,
            Notes = "Patient returned to waiting area and re-entered priority queue",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();
        return MapToResponse(token, 0, 0);
    }

    // =========================================================================
    // 7. COMPLETE CONSULTATION (Enter Notes & Close Visit)
    // =========================================================================
    public async Task<QueueTokenResponse> CompleteConsultationAsync(Guid tokenId, Guid doctorUserId, CompleteConsultationRequest request)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
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
    // 8. RECALCULATE QUEUE SCORES (Anti-Starvation Aging Engine Loop)
    // =========================================================================
    public async Task RecalculateQueueScoresAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;

        var waitingTokens = await _context.QueueTokens
            .Where(t => t.Status == "Waiting" && t.BookedAtUtc.Date == todayUtc && t.TriagedAtUtc.HasValue)
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

    // =========================================================================
    // PRIVATE HELPER: MAPPING
    // =========================================================================
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
