using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

public class TriageService : ITriageService
{
    private readonly VitalQDbContext _context;

    public TriageService(VitalQDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // 1. RECORD TRIAGE ASSESSMENT (Vitals Intake & Urgency Assignment)
    // =========================================================================
    public async Task<QueueTokenResponse> RecordTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request)
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
            throw new KeyNotFoundException($"Queue token with ID {tokenId} was not found.");
        }

        if (token.Status == "Completed" || token.Status == "Cancelled")
        {
            throw new InvalidOperationException($"Cannot triage a token that is already {token.Status}.");
        }

        // Determine triage level (Automated CTAS criteria or Nurse Clinical Override)
        string triageLevel;
        if (request.IsManualOverride && !string.IsNullOrWhiteSpace(request.OverrideTriageLevel))
        {
            var cleanOverride = request.OverrideTriageLevel.Trim();
            if (cleanOverride != "Red" && cleanOverride != "Yellow" && cleanOverride != "Green")
            {
                throw new ArgumentException("Manual override triage level must be 'Red', 'Yellow', or 'Green'.");
            }

            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new ArgumentException("A clinical override reason is mandatory when manually setting triage level.");
            }

            triageLevel = cleanOverride;
        }
        else
        {
            triageLevel = CalculateAutomatedTriageLevel(
                request.SpO2,
                request.SystolicBp,
                request.HeartRate,
                request.Temperature,
                request.PainScale);
        }

        var baseWeight = CalculateBaseWeight(triageLevel);

        // Upsert TriageAssessment (1:1 with QueueToken)
        var assessment = await _context.TriageAssessments.FirstOrDefaultAsync(a => a.QueueTokenId == tokenId);
        if (assessment == null)
        {
            assessment = new TriageAssessment
            {
                Id = Guid.NewGuid(),
                QueueTokenId = tokenId,
                NurseUserId = nurseUserId,
                NursingStationId = request.NursingStationId,
                SpO2 = request.SpO2,
                SystolicBp = request.SystolicBp,
                DiastolicBp = request.DiastolicBp,
                HeartRate = request.HeartRate,
                Temperature = request.Temperature,
                PainScale = request.PainScale,
                TriageLevel = triageLevel,
                IsManualOverride = request.IsManualOverride,
                OverrideReason = request.IsManualOverride ? request.OverrideReason?.Trim() : null,
                NurseNotes = request.NurseNotes?.Trim(),
                AssessedAtUtc = DateTime.UtcNow
            };
            await _context.TriageAssessments.AddAsync(assessment);
        }
        else
        {
            assessment.NurseUserId = nurseUserId;
            assessment.NursingStationId = request.NursingStationId;
            assessment.SpO2 = request.SpO2;
            assessment.SystolicBp = request.SystolicBp;
            assessment.DiastolicBp = request.DiastolicBp;
            assessment.HeartRate = request.HeartRate;
            assessment.Temperature = request.Temperature;
            assessment.PainScale = request.PainScale;
            assessment.TriageLevel = triageLevel;
            assessment.IsManualOverride = request.IsManualOverride;
            assessment.OverrideReason = request.IsManualOverride ? request.OverrideReason?.Trim() : null;
            assessment.NurseNotes = request.NurseNotes?.Trim();
            assessment.AssessedAtUtc = DateTime.UtcNow;
        }

        // Transition QueueToken to "Waiting" with CTAS initial BaseWeight
        var previousStatus = token.Status;
        token.Status = "Waiting";
        token.BaseWeight = baseWeight;
        token.PriorityScore = baseWeight; // At triage time, elapsed = 0 so score = baseWeight
        token.TriagedAtUtc = DateTime.UtcNow;

        // Stamp audit trail
        var audit = new TokenAuditLog
        {
            Id = Guid.NewGuid(),
            QueueTokenId = tokenId,
            PreviousStatus = previousStatus,
            NewStatus = "Waiting",
            ChangeSource = "Nurse",
            ChangedByUserId = nurseUserId,
            Notes = $"Clinical Triage: {triageLevel} (Base Weight: {baseWeight}). Vitals: BP {request.SystolicBp}/{request.DiastolicBp}, SpO2 {request.SpO2}%, HR {request.HeartRate}, Temp {request.Temperature}°C, Pain {request.PainScale}/10." +
                    (request.IsManualOverride ? $" Manual Override Reason: {request.OverrideReason}" : ""),
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();

        // Calculate live position ahead
        var patientsAhead = await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.DoctorId == token.DoctorId &&
                        t.Status == "Waiting" &&
                        t.Id != token.Id &&
                        (t.PriorityScore > token.PriorityScore ||
                         (t.PriorityScore == token.PriorityScore && t.BookedAtUtc < token.BookedAtUtc)))
            .CountAsync();

        var avgConsultationMins = token.Doctor?.AvgConsultationMinutes ?? 10;

        return MapToResponse(token, triageLevel, patientsAhead, avgConsultationMins);
    }

    // =========================================================================
    // 2. GET PENDING TRIAGE TOKENS (Tokens waiting for nurse vitals intake)
    // =========================================================================
    public async Task<IEnumerable<QueueTokenResponse>> GetPendingTriageTokensAsync(Guid? departmentId = null)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var query = _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
                .ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.Status == "Booked" && t.BookedAtUtc.Date == todayUtc);

        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            query = query.Where(t => t.DepartmentId == departmentId.Value);
        }

        var tokens = await query
            .OrderBy(t => t.BookedAtUtc)
            .ToListAsync();

        return tokens.Select(t => MapToResponse(t, null, 0, t.Doctor?.AvgConsultationMinutes ?? 10));
    }

    // =========================================================================
    // 3. GET TRIAGE ASSESSMENT DETAILS BY TOKEN ID
    // =========================================================================
    public async Task<TriageAssessmentResponse?> GetTriageAssessmentByTokenIdAsync(Guid tokenId)
    {
        var assessment = await _context.TriageAssessments
            .AsNoTracking()
            .Include(a => a.NurseUser)
            .Include(a => a.NursingStation)
            .FirstOrDefaultAsync(a => a.QueueTokenId == tokenId);

        if (assessment == null) return null;

        return new TriageAssessmentResponse
        {
            Id = assessment.Id,
            QueueTokenId = assessment.QueueTokenId,
            NurseUserId = assessment.NurseUserId,
            NurseName = assessment.NurseUser?.FullName ?? string.Empty,
            NursingStationId = assessment.NursingStationId,
            StationName = assessment.NursingStation?.StationName,
            SpO2 = assessment.SpO2,
            SystolicBp = assessment.SystolicBp,
            DiastolicBp = assessment.DiastolicBp,
            HeartRate = assessment.HeartRate,
            Temperature = assessment.Temperature,
            PainScale = assessment.PainScale,
            TriageLevel = assessment.TriageLevel,
            IsManualOverride = assessment.IsManualOverride,
            OverrideReason = assessment.OverrideReason,
            NurseNotes = assessment.NurseNotes,
            AssessedAtUtc = assessment.AssessedAtUtc
        };
    }

    // =========================================================================
    // 4. SEARCH PATIENTS FOR NURSE DESK (With active token status)
    // =========================================================================
    public async Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Enumerable.Empty<PatientSearchResult>();
        }

        var q = query.Trim().ToLower();
        var todayUtc = DateTime.UtcNow.Date;

        var patients = await _context.Patients
            .AsNoTracking()
            .Include(p => p.QueueTokens)
            .Where(p => p.PhoneNumber.Contains(q) ||
                        p.MedicalRecordNumber.ToLower().Contains(q) ||
                        p.FullName.ToLower().Contains(q))
            .Take(15)
            .ToListAsync();

        return patients.Select(p =>
        {
            var activeToken = p.QueueTokens
                .Where(t => t.BookedAtUtc.Date == todayUtc && t.Status != "Completed" && t.Status != "Cancelled")
                .OrderByDescending(t => t.BookedAtUtc)
                .FirstOrDefault();

            return new PatientSearchResult
            {
                PatientId = p.Id,
                MedicalRecordNumber = p.MedicalRecordNumber,
                FullName = p.FullName,
                PhoneNumber = p.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                ActiveTokenId = activeToken?.Id,
                ActiveTokenNumber = activeToken?.TokenNumber,
                ActiveTokenStatus = activeToken?.Status
            };
        });
    }

    // =========================================================================
    // 5. AUTOMATED CTAS TRIAGE LEVEL CALCULATION (§05 VitalQ Hospital Spec)
    // =========================================================================
    public string CalculateAutomatedTriageLevel(decimal spo2, int systolicBp, int heartRate, decimal temperature, int painScale)
    {
        // Red (Emergency, Base Weight 100): Immediate life-threat
        if (spo2 < 90 || systolicBp < 90 || systolicBp > 180 || heartRate > 120 || heartRate < 40 || temperature > 39.5m || painScale == 10)
        {
            return "Red";
        }

        // Yellow (Urgent, Base Weight 50): Potential acute deterioration (< 30 min target)
        if (spo2 <= 94 || systolicBp >= 160 || heartRate >= 100 || temperature >= 38.0m || painScale >= 6)
        {
            return "Yellow";
        }

        // Green (Routine, Base Weight 10): Stable clinical parameters (< 120 min target)
        return "Green";
    }

    // =========================================================================
    // 6. BASE WEIGHT LOOKUP
    // =========================================================================
    public int CalculateBaseWeight(string triageLevel)
    {
        return triageLevel switch
        {
            "Red" => PriorityScoreCalculator.BaseWeightRed,
            "Yellow" => PriorityScoreCalculator.BaseWeightYellow,
            _ => PriorityScoreCalculator.BaseWeightGreen
        };
    }

    // =========================================================================
    // PRIVATE HELPER: MAPPING
    // =========================================================================
    private static QueueTokenResponse MapToResponse(QueueToken t, string? triageLevel, int patientsAhead, int avgConsultationMins) => new()
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
        TriageLevel = triageLevel ?? t.TriageAssessment?.TriageLevel,
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
