using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Common;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Nurse station service running on PC at the clinic reception & triage desk.
/// Handles patient intake, walk-in registration, counter token booking, and vitals triage.
/// </summary>
public class NurseService : INurseService
{
    private readonly VitalQDbContext _context;
    private readonly ITokenGenerator _tokenGenerator;

    public NurseService(VitalQDbContext context, ITokenGenerator tokenGenerator)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    // =========================================================================
    // 1. SEARCH PATIENTS FOR NURSE DESK (With active token status)
    // =========================================================================
    public async Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Enumerable.Empty<PatientSearchResult>();
        }

        var q = query.Trim().ToLower();
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var patients = await _context.Patients
            .AsNoTracking()
            .Include(p => p.QueueTokens.Where(t => t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.Status != "Completed" && t.Status != "Cancelled"))
            .Where(p => p.PhoneNumber.Contains(q) ||
                        p.MedicalRecordNumber.ToLower().Contains(q) ||
                        p.FullName.ToLower().Contains(q))
            .Take(15)
            .ToListAsync();

        return patients.Select(p =>
        {
            var activeToken = p.QueueTokens
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
    // 2. REGISTER WALK-IN PATIENT (In-person at Clinic Reception Desk)
    // =========================================================================
    public async Task<PatientResponse> RegisterWalkInPatientAsync(WalkInRegisterRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);
        var dobPassword = request.DateOfBirth.ToString("ddMMyyyy");

        // Ensure User account exists (so patient can log in later via Mobile/PC with Phone + DOB)
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == phone);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = phone,
                Password = dobPassword,
                FullName = request.FullName.Trim(),
                PhoneNumber = phone,
                Role = "Patient",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _context.Users.AddAsync(user);
        }

        // Link primary UserId if this user account does not yet have a profile
        var alreadyLinked = await _context.Patients.AnyAsync(p => p.UserId == user.Id);
        Guid? linkedUserId = alreadyLinked ? null : user.Id;

        // Create Medical Record
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = linkedUserId,
            MedicalRecordNumber = MrnGenerator.Generate(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.Patients.AddAsync(patient);
        await _context.SaveChangesAsync();

        return new PatientResponse
        {
            Id = patient.Id,
            UserId = patient.UserId,
            MedicalRecordNumber = patient.MedicalRecordNumber,
            FullName = patient.FullName,
            PhoneNumber = patient.PhoneNumber,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            CreatedAtUtc = patient.CreatedAtUtc
        };
    }

    // =========================================================================
    // 3. BOOK WALK-IN TOKEN (Issued by Nurse at Reception Counter)
    // =========================================================================
    public async Task<QueueTokenResponse> BookWalkInTokenAsync(BookTokenRequest request)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PatientId)
            ?? throw new KeyNotFoundException("Patient profile was not found.");

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

        var today = DateTime.UtcNow.Date;
        var alreadyBooked = await _context.QueueTokens
            .AsNoTracking()
            .AnyAsync(t => t.PatientId == patient.Id
                        && t.DoctorId == doctor.Id
                        && t.BookedAtUtc >= today
                        && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"));

        if (alreadyBooked)
        {
            throw new InvalidOperationException($"Patient '{patient.FullName}' already has an active appointment with Dr. {doctor.User.FullName} today.");
        }

        // Atomic Token Sequence
        var tokenNumber = await _tokenGenerator.GenerateTokenNumberAsync(doctor.DepartmentId, doctor.Department.Code);

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
            ChangeSource = "Nurse",
            ChangedAtUtc = DateTime.UtcNow,
            Notes = $"Walk-in token {tokenNumber} booked at desk for {patient.FullName}"
        };

        await _context.QueueTokens.AddAsync(queueToken);
        await _context.TokenAuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();

        return MapToTokenResponse(queueToken, patient, doctor);
    }

    // =========================================================================
    // 4. GET PENDING TRIAGE TOKENS (Booked today, awaiting nurse vitals)
    // =========================================================================
    public async Task<IEnumerable<QueueTokenResponse>> GetPendingTriageQueueAsync(Guid? departmentId = null)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var query = _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .Where(t => t.Status == "Booked" && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc);

        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            query = query.Where(t => t.DepartmentId == departmentId.Value);
        }

        var tokens = await query.OrderBy(t => t.BookedAtUtc).ToListAsync();
        return tokens.Select(t => MapToTokenResponse(t, t.Patient!, t.Doctor!));
    }

    // =========================================================================
    // 5. RECORD VITALS & TRIAGE (Calculate CTAS & move to Waiting queue)
    // =========================================================================
    public async Task<QueueTokenResponse> RecordVitalsAndTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department)
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

        // Determine triage level
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

        // Upsert Triage Assessment
        var assessment = await _context.TriageAssessments.FirstOrDefaultAsync(a => a.QueueTokenId == tokenId);
        if (assessment == null)
        {
            assessment = new TriageAssessment
            {
                Id = Guid.NewGuid(),
                QueueTokenId = tokenId,
                NurseUserId = nurseUserId,
                AssessedAtUtc = DateTime.UtcNow
            };
            await _context.TriageAssessments.AddAsync(assessment);
        }

        assessment.SpO2 = request.SpO2;
        assessment.SystolicBp = request.SystolicBp;
        assessment.DiastolicBp = request.DiastolicBp;
        assessment.HeartRate = request.HeartRate;
        assessment.Temperature = request.Temperature;
        assessment.PainScale = request.PainScale;
        assessment.TriageLevel = triageLevel;
        assessment.IsManualOverride = request.IsManualOverride;
        assessment.OverrideReason = request.OverrideReason?.Trim();
        assessment.NurseNotes = request.NurseNotes?.Trim();
        assessment.AssessedAtUtc = DateTime.UtcNow;

        // Transition token to 'Waiting' in doctor's queue
        var previousStatus = token.Status;
        token.Status = "Waiting";
        token.BaseWeight = baseWeight;
        token.PriorityScore = baseWeight;
        token.TriagedAtUtc = DateTime.UtcNow;

        // Audit Trail
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

        // Calculate position ahead
        var patientsAhead = await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.DoctorId == token.DoctorId &&
                        t.Status == "Waiting" &&
                        t.Id != token.Id &&
                        (t.PriorityScore > token.PriorityScore ||
                         (t.PriorityScore == token.PriorityScore && t.BookedAtUtc < token.BookedAtUtc)))
            .CountAsync();

        var avgConsultationMins = token.Doctor?.AvgConsultationMinutes ?? 10;
        var response = MapToTokenResponse(token, token.Patient, token.Doctor);
        response.PatientsAhead = patientsAhead;
        response.EstimatedWaitMinutes = patientsAhead * avgConsultationMins;
        response.TriageLevel = triageLevel;

        return response;
    }

    // =========================================================================
    // 6. GET TRIAGE ASSESSMENT DETAILS
    // =========================================================================
    public async Task<TriageAssessmentResponse?> GetTriageAssessmentAsync(Guid tokenId)
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

    // CTAS Clinical Rules
    private static string CalculateAutomatedTriageLevel(decimal spo2, int systolicBp, int heartRate, decimal temperature, int painScale)
    {
        if (spo2 < 90m || systolicBp < 80 || heartRate > 150 || temperature > 40.5m)
            return "Red";

        if ((spo2 >= 90m && spo2 <= 94m) ||
            (systolicBp >= 80 && systolicBp <= 89) ||
            (heartRate >= 121 && heartRate <= 150) ||
            heartRate < 45 ||
            (temperature >= 39.0m && temperature <= 40.5m) ||
            painScale >= 7)
            return "Yellow";

        return "Green";
    }

    private static int CalculateBaseWeight(string triageLevel) => triageLevel switch
    {
        "Red" => 1000,
        "Yellow" => 500,
        "Green" => 100,
        _ => 100
    };

    private static string NormalizePhone(string rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            throw new ArgumentException("Phone number is required.");
        var cleaned = Regex.Replace(rawPhone.Trim(), @"[^\d]", "");
        if (cleaned.Length < 7 || cleaned.Length > 15)
            throw new ArgumentException("Please enter a valid phone number (7 to 15 digits).");
        return cleaned;
    }

    private static void ValidateDateOfBirth(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob >= today)
            throw new ArgumentException("Date of birth must be in the past.");
        if (dob < today.AddYears(-130))
            throw new ArgumentException("Please provide a valid date of birth.");
    }

    private static QueueTokenResponse MapToTokenResponse(QueueToken t, Patient? p, Doctor? d) => new()
    {
        Id = t.Id,
        TokenNumber = t.TokenNumber,
        PatientId = t.PatientId,
        PatientName = p?.FullName ?? string.Empty,
        PatientPhone = p?.PhoneNumber ?? string.Empty,
        DepartmentId = t.DepartmentId,
        DepartmentName = d?.Department?.Name ?? t.Department?.Name ?? string.Empty,
        DepartmentCode = d?.Department?.Code ?? t.Department?.Code ?? string.Empty,
        DoctorId = t.DoctorId,
        DoctorName = d?.User?.FullName ?? string.Empty,
        RoomNumber = d?.RoomNumber ?? string.Empty,
        Status = t.Status,
        PriorityScore = t.PriorityScore,
        BaseWeight = t.BaseWeight,
        TriageLevel = t.TriageAssessment?.TriageLevel,
        BookedAtUtc = t.BookedAtUtc,
        RowVersion = t.RowVersion ?? Array.Empty<byte>()
    };
}
