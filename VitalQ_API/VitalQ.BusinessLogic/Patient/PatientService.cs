using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Common;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Service powering the Patient Portal on Mobile and PC browser.
/// Handles patient registration, family profiles, appointment booking, active tokens, and visit history.
/// </summary>
public class PatientService : IPatientService
{
    private readonly VitalQDbContext _context;
    private readonly IAuthService _authService;
    private readonly ITokenGenerator _tokenGenerator;

    public PatientService(VitalQDbContext context, IAuthService authService, ITokenGenerator tokenGenerator)
    {
        _context = context;
        _authService = authService;
        _tokenGenerator = tokenGenerator;
    }

    // =========================================================================
    // 1. PATIENT SELF-REGISTRATION (Online / Mobile App)
    // =========================================================================
    public async Task<AuthResponse> SelfRegisterAsync(SelfRegisterRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);

        var alreadyRegistered = await _context.Users.AnyAsync(u => u.Username == phone);
        if (alreadyRegistered)
        {
            throw new InvalidOperationException("An account with this phone number already exists. Please log in.");
        }

        var dobPassword = request.DateOfBirth.ToString("ddMMyyyy");

        var user = new User
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

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            MedicalRecordNumber = MrnGenerator.Generate(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Centralized JWT token generation
        var authResponse = _authService.GenerateToken(user);

        await _context.Users.AddAsync(user);
        await _context.Patients.AddAsync(patient);
        await _context.SaveChangesAsync();

        return authResponse;
    }

    // =========================================================================
    // 2. ADD FAMILY MEMBER (Logged-in Patient adds Dependent)
    // =========================================================================
    public async Task<PatientResponse> AddFamilyMemberAsync(Guid currentUserId, AddFamilyMemberRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);

        var parentUser = await _context.Users.FindAsync(currentUserId);
        if (parentUser == null)
        {
            throw new ArgumentException("Parent account not found.");
        }

        var familyMember = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = null,
            MedicalRecordNumber = MrnGenerator.Generate(),
            FullName = request.FullName.Trim(),
            PhoneNumber = parentUser.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.Patients.AddAsync(familyMember);
        await _context.SaveChangesAsync();

        return MapToResponse(familyMember);
    }

    // =========================================================================
    // 3. GET MY FAMILY (List all profiles under parent's phone number)
    // =========================================================================
    public async Task<IEnumerable<PatientResponse>> GetMyFamilyMembersAsync(Guid currentUserId)
    {
        var parentUser = await _context.Users.FindAsync(currentUserId);
        if (parentUser == null) return Enumerable.Empty<PatientResponse>();

        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber == parentUser.PhoneNumber)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    // =========================================================================
    // 4. GET PATIENT BY ID
    // =========================================================================
    public async Task<PatientResponse?> GetPatientByIdAsync(Guid patientId)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patientId);

        return patient == null ? null : MapToResponse(patient);
    }

    // =========================================================================
    // 5. GET PATIENT PROFILE BY USER ID
    // =========================================================================
    public async Task<PatientResponse?> GetPatientByUserIdAsync(Guid userId)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        return patient == null ? null : MapToResponse(patient);
    }

    // =========================================================================
    // 6. UPDATE PATIENT PROFILE
    // =========================================================================
    public async Task<PatientResponse> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);

        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null)
        {
            throw new KeyNotFoundException($"Patient with ID {patientId} was not found.");
        }

        if (patient.UserId.HasValue)
        {
            var user = await _context.Users.FindAsync(patient.UserId.Value);
            if (user != null)
            {
                if (user.PhoneNumber != phone)
                {
                    var phoneExists = await _context.Users.AnyAsync(u => u.Id != user.Id && u.Username == phone);
                    if (phoneExists)
                    {
                        throw new InvalidOperationException("This phone number is already registered to another user account.");
                    }

                    var dependents = await _context.Patients
                        .Where(p => p.PhoneNumber == user.PhoneNumber && p.Id != patient.Id && p.UserId == null)
                        .ToListAsync();

                    foreach (var dep in dependents)
                    {
                        dep.PhoneNumber = phone;
                    }

                    user.PhoneNumber = phone;
                    user.Username = phone;
                }

                user.FullName = request.FullName.Trim();
                user.Password = request.DateOfBirth.ToString("ddMMyyyy");
            }
        }

        patient.FullName = request.FullName.Trim();
        patient.PhoneNumber = phone;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Gender = request.Gender.Trim();

        await _context.SaveChangesAsync();
        return MapToResponse(patient);
    }

    // =========================================================================
    // 6. GET PATIENT VISIT HISTORY
    // =========================================================================
    public async Task<IEnumerable<PatientVisitHistoryDto>> GetPatientVisitHistoryAsync(Guid patientId)
    {
        return await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.PatientId == patientId && t.Status == "Completed")
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department)
            .Include(t => t.TriageAssessment)
            .OrderByDescending(t => t.BookedAtUtc)
            .Select(t => new PatientVisitHistoryDto
            {
                TokenId = t.Id,
                TokenNumber = t.TokenNumber,
                PatientId = t.PatientId,
                PatientName = t.Patient.FullName,
                DoctorName = t.Doctor.User.FullName,
                DepartmentName = t.Doctor.Department.Name,
                Status = t.Status,
                TriageLevel = t.TriageAssessment != null ? t.TriageAssessment.TriageLevel : "Green",
                BookedAtUtc = t.BookedAtUtc,
                CalledAtUtc = t.CalledAtUtc,
                CompletedAtUtc = t.CompletedAtUtc,
                ConsultationNotes = t.ConsultationNotes
            })
            .ToListAsync();
    }

    // =========================================================================
    // 7. BOOK APPOINTMENT SLOT (Online via Mobile or PC)
    // =========================================================================
    public async Task<QueueTokenResponse> BookAppointmentAsync(BookTokenRequest request, Guid currentUserId)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PatientId)
            ?? throw new KeyNotFoundException("Patient profile was not found.");

        var user = await _context.Users.FindAsync(currentUserId);
        if (user != null && user.Role == "Patient")
        {
            bool isAllowed = patient.UserId == currentUserId || patient.PhoneNumber == user.PhoneNumber;
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException("You can only book appointments for yourself or your family dependents.");
            }
        }

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
        var tomorrow = today.AddDays(1);
        var alreadyBooked = await _context.QueueTokens
            .AsNoTracking()
            .AnyAsync(t => t.PatientId == patient.Id
                        && t.DoctorId == doctor.Id
                        && t.BookedAtUtc >= today
                        && t.BookedAtUtc < tomorrow
                        && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"));

        if (alreadyBooked)
        {
            throw new InvalidOperationException($"Patient '{patient.FullName}' already has an active appointment with Dr. {doctor.User.FullName} today.");
        }

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
            ChangeSource = "Patient",
            ChangedByUserId = currentUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Notes = $"Online booking: Token {tokenNumber} booked for {patient.FullName}"
        };

        await _context.QueueTokens.AddAsync(queueToken);
        await _context.TokenAuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();

        return MapToTokenResponse(queueToken, patient, doctor);
    }

    // =========================================================================
    // 8. GET ACTIVE TOKENS TODAY FOR USER & FAMILY (Mobile/PC live status)
    // =========================================================================
    public async Task<IEnumerable<QueueTokenResponse>> GetMyActiveTokensAsync(Guid currentUserId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null) return Enumerable.Empty<QueueTokenResponse>();

        var familyPatientIds = await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber == user.PhoneNumber)
            .Select(p => p.Id)
            .ToListAsync();

        if (!familyPatientIds.Any()) return Enumerable.Empty<QueueTokenResponse>();

        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .Where(t => familyPatientIds.Contains(t.PatientId)
                     && t.BookedAtUtc >= today
                     && t.BookedAtUtc < tomorrow
                     && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"))
            .OrderBy(t => t.BookedAtUtc)
            .ToListAsync();

        var responseList = new List<QueueTokenResponse>();
        foreach (var token in tokens)
        {
            var res = MapToTokenResponse(token, token.Patient!, token.Doctor!);

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

            responseList.Add(res);
        }

        return responseList;
    }

    // =========================================================================
    // 9. GET SPECIFIC PATIENT ACTIVE TOKEN TODAY
    // =========================================================================
    public async Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .Where(t => t.PatientId == patientId
                     && t.BookedAtUtc >= today
                     && t.BookedAtUtc < tomorrow
                     && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"))
            .OrderByDescending(t => t.BookedAtUtc)
            .FirstOrDefaultAsync();

        if (token == null) return null;

        var res = MapToTokenResponse(token, token.Patient!, token.Doctor!);

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

            var avgConsult = token.Doctor?.AvgConsultationMinutes ?? 10;
            res.EstimatedWaitMinutes = res.PatientsAhead * avgConsult;
        }
        else if (token.Status == "Called")
        {
            res.PatientsAhead = 0;
            res.EstimatedWaitMinutes = 0;
        }

        return res;
    }

    // =========================================================================
    // 10. CANCEL APPOINTMENT
    // =========================================================================
    public async Task<QueueTokenResponse> CancelMyTokenAsync(Guid tokenId, Guid currentUserId, string? reason = null)
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
            ChangeSource = "Patient",
            ChangedByUserId = currentUserId,
            Notes = string.IsNullOrWhiteSpace(reason) ? "Cancelled by patient" : $"Cancelled: {reason.Trim()}",
            ChangedAtUtc = DateTime.UtcNow
        };
        await _context.TokenAuditLogs.AddAsync(audit);

        await _context.SaveChangesAsync();

        return MapToTokenResponse(token, token.Patient, token.Doctor);
    }

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

    private static PatientResponse MapToResponse(Patient p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        MedicalRecordNumber = p.MedicalRecordNumber,
        FullName = p.FullName,
        PhoneNumber = p.PhoneNumber,
        DateOfBirth = p.DateOfBirth,
        Gender = p.Gender,
        CreatedAtUtc = p.CreatedAtUtc
    };

    private static QueueTokenResponse MapToTokenResponse(QueueToken t, Patient p, Doctor d) => new()
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
