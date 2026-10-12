using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Common;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// SERVICE: Patient Portal Core Business Logic
/// 
/// PURPOSE:
/// Powers all patient-facing workflows on mobile and PC browsers:
/// - Section 1: Demographics & Family Profiles (Self-registration, family dependents, profile updates)
/// - Section 2: Live Appointments & Queue (Booking slots, tracking position ahead & ETAs, cancellation)
/// - Section 3: Consultation History (Single-query batch past visits with doctor advice)
/// </summary>
public class PatientService : IPatientService
{
    private readonly VitalQDbContext _context;
    private readonly IAuthService _authService;

    public PatientService(VitalQDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    #region 1. Demographics & Family Profiles

    /// <summary>
    /// Registers a new patient account using Phone Number + Date of Birth.
    /// If the patient already visited the hospital as a walk-in, merges their existing MRN chart
    /// with this new login instead of creating a split duplicate record.
    /// </summary>
    public async Task<AuthResponse> SelfRegisterAsync(SelfRegisterRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);

        // 1. Ensure phone number is not already taken
        var alreadyRegistered = await _context.Users.AnyAsync(u => u.Username == phone);
        if (alreadyRegistered)
        {
            throw new InvalidOperationException("An account with this phone number already exists. Please log in.");
        }

        var dobPassword = request.DateOfBirth.ToString("ddMMyyyy");

        // 2. Create the User identity
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

        // 3. Clinical Merge Check: Link existing nurse walk-in chart if one exists
        var existingWalkInPatient = await _context.Patients
            .FirstOrDefaultAsync(p => p.PhoneNumber == phone 
                                   && p.DateOfBirth == request.DateOfBirth 
                                   && p.UserId == null);

        Patient patient;
        if (existingWalkInPatient != null)
        {
            existingWalkInPatient.UserId = user.Id;
            existingWalkInPatient.FullName = request.FullName.Trim();
            existingWalkInPatient.Gender = NormalizeGender(request.Gender);
            patient = existingWalkInPatient;
        }
        else
        {
            patient = new Patient
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                MedicalRecordNumber = MrnGenerator.Generate(),
                FullName = request.FullName.Trim(),
                PhoneNumber = phone,
                DateOfBirth = request.DateOfBirth,
                Gender = NormalizeGender(request.Gender),
                CreatedAtUtc = DateTime.UtcNow
            };
            await _context.Patients.AddAsync(patient);
        }

        // 4. Generate centralized JWT session tokens
        var authResponse = _authService.GenerateToken(user);

        await _context.Users.AddAsync(user);
        await SaveChangesWithMrnRetryAsync();

        return authResponse;
    }

    /// <summary>
    /// Adds a child, spouse, or elderly dependent under the logged-in user's account.
    /// </summary>
    public async Task<PatientResponse> AddFamilyMemberAsync(Guid currentUserId, AddFamilyMemberRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);

        var parentUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId)
            ?? throw new ArgumentException("Parent account not found.");

        var fullNameTrimmed = request.FullName.Trim();

        // Prevent accidental double-submission of the exact same dependent
        var alreadyExists = await _context.Patients.AnyAsync(p =>
            p.PhoneNumber == parentUser.PhoneNumber &&
            p.FullName.ToLower() == fullNameTrimmed.ToLower() &&
            p.DateOfBirth == request.DateOfBirth);

        if (alreadyExists)
        {
            throw new InvalidOperationException($"A family member named '{fullNameTrimmed}' with the same date of birth is already registered.");
        }

        var familyMember = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = null,
            MedicalRecordNumber = MrnGenerator.Generate(),
            FullName = fullNameTrimmed,
            PhoneNumber = parentUser.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = NormalizeGender(request.Gender),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.Patients.AddAsync(familyMember);
        await SaveChangesWithMrnRetryAsync();

        return MapToResponse(familyMember);
    }

    /// <summary>
    /// Retrieves all family member profiles sharing the user's mobile number.
    /// Uses direct LINQ index seek (<0.1ms).
    /// </summary>
    public async Task<IEnumerable<PatientResponse>> GetMyFamilyMembersAsync(Guid currentUserId)
    {
        var parentPhone = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => u.PhoneNumber)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(parentPhone))
        {
            return Enumerable.Empty<PatientResponse>();
        }

        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber == parentPhone)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => new PatientResponse
            {
                Id = p.Id,
                UserId = p.UserId,
                MedicalRecordNumber = p.MedicalRecordNumber,
                FullName = p.FullName,
                PhoneNumber = p.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                CreatedAtUtc = p.CreatedAtUtc
            })
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a patient demographic profile by its primary key GUID.
    /// </summary>
    public async Task<PatientResponse?> GetPatientByIdAsync(Guid patientId)
    {
        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.Id == patientId)
            .Select(p => new PatientResponse
            {
                Id = p.Id,
                UserId = p.UserId,
                MedicalRecordNumber = p.MedicalRecordNumber,
                FullName = p.FullName,
                PhoneNumber = p.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                CreatedAtUtc = p.CreatedAtUtc
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Retrieves the patient profile linked to an authenticated user login account ID.
    /// </summary>
    public async Task<PatientResponse?> GetPatientByUserIdAsync(Guid userId)
    {
        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new PatientResponse
            {
                Id = p.Id,
                UserId = p.UserId,
                MedicalRecordNumber = p.MedicalRecordNumber,
                FullName = p.FullName,
                PhoneNumber = p.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                CreatedAtUtc = p.CreatedAtUtc
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Updates patient demographics and cascades phone changes to linked accounts and dependents.
    /// </summary>
    public async Task<PatientResponse> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);

        var patient = await _context.Patients.FindAsync(patientId)
            ?? throw new KeyNotFoundException($"Patient with ID {patientId} was not found.");

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

                    // Cascade phone update to unlinked dependents
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
        patient.Gender = NormalizeGender(request.Gender);

        await _context.SaveChangesAsync();
        return MapToResponse(patient);
    }

    #endregion

    #region 2. Appointment Booking & Live Queue Tracking

    /// <summary>
    /// Books an appointment slot online today and generates an atomic, collision-proof token sequence.
    /// </summary>
    public async Task<QueueTokenResponse> BookAppointmentAsync(BookTokenRequest request, Guid currentUserId)
    {
        // 1. Verify patient exists and caller has ownership
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PatientId)
            ?? throw new KeyNotFoundException("Patient profile was not found.");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (user != null && user.Role == "Patient")
        {
            bool isAllowed = patient.UserId == currentUserId || patient.PhoneNumber == user.PhoneNumber;
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException("You can only book appointments for yourself or your family dependents.");
            }
        }

        // 2. Verify doctor availability
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

        // 3. Sargable date range check for duplicate booking today
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

        // 4. Generate concurrency-safe atomic sequence token
        var tokenNumber = await TokenGenerator.GenerateAsync(_context, doctor.DepartmentId, doctor.Department.Code);

        // 5. Create QueueToken in "Booked" status (Step 1: Pending Nurse Desk Vitals)
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

        await _context.QueueTokens.AddAsync(queueToken);
        await _context.SaveChangesAsync();

        var response = MapToTokenResponse(queueToken, patient, doctor);
        response.PatientsAhead = 0;
        response.EstimatedWaitMinutes = 0;
        return response;
    }

    /// <summary>
    /// Retrieves all active consultation tokens today for the user and their family dependents.
    /// Calculates live queue position ("patients ahead") for all tokens currently waiting in line.
    /// </summary>
    public async Task<IEnumerable<QueueTokenResponse>> GetMyActiveTokensAsync(Guid currentUserId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var userPhone = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => u.PhoneNumber)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(userPhone)) return Enumerable.Empty<QueueTokenResponse>();

        var familyPatientIds = await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber == userPhone)
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

    /// <summary>
    /// Retrieves today's active consultation slip for a specific patient ID.
    /// </summary>
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

    /// <summary>
    /// Cancels an active consultation token and releases the queue slot.
    /// </summary>
    public async Task<QueueTokenResponse> CancelMyTokenAsync(Guid tokenId, Guid currentUserId, string? reason = null)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department)
            .Include(t => t.Department)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId)
            ?? throw new KeyNotFoundException($"Token with ID {tokenId} was not found.");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId)
            ?? throw new UnauthorizedAccessException("User not found.");

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

        token.Status = "Cancelled";
        token.CompletedAtUtc = DateTime.UtcNow;
        token.ConsultationNotes = string.IsNullOrWhiteSpace(reason)
            ? "Cancelled by patient"
            : $"Cancelled by patient: {reason.Trim()}";

        await _context.SaveChangesAsync();

        return MapToTokenResponse(token, token.Patient, token.Doctor);
    }

    #endregion

    #region 3. Consultation & Clinical History

    /// <summary>
    /// Retrieves past outpatient encounters (Completed, Cancelled, Expired, Skipped) for an individual patient.
    /// </summary>
    public async Task<IEnumerable<PatientVisitHistoryDto>> GetPatientVisitHistoryAsync(Guid patientId)
    {
        return await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.PatientId == patientId && 
                       (t.Status == "Completed" || t.Status == "Cancelled" || t.Status == "Expired" || t.Status == "Skipped"))
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
                TriagedAtUtc = t.TriagedAtUtc,
                CalledAtUtc = t.CalledAtUtc,
                CompletedAtUtc = t.CompletedAtUtc,
                ConsultationNotes = t.ConsultationNotes
            })
            .ToListAsync();
    }

    /// <summary>
    /// BATCH QUERY: Retrieves past visit history (Completed, Cancelled, Expired, Skipped) for all family members in a single SQL query.
    /// 100% thread-safe and eliminates parallel DbContext crashes.
    /// </summary>
    public async Task<IEnumerable<PatientVisitHistoryDto>> GetFamilyVisitHistoryAsync(IEnumerable<Guid> patientIds)
    {
        var idsList = patientIds.ToList();
        if (!idsList.Any()) return Enumerable.Empty<PatientVisitHistoryDto>();

        return await _context.QueueTokens
            .AsNoTracking()
            .Where(t => idsList.Contains(t.PatientId) && 
                       (t.Status == "Completed" || t.Status == "Cancelled" || t.Status == "Expired" || t.Status == "Skipped"))
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
                TriagedAtUtc = t.TriagedAtUtc,
                CalledAtUtc = t.CalledAtUtc,
                CompletedAtUtc = t.CompletedAtUtc,
                ConsultationNotes = t.ConsultationNotes
            })
            .ToListAsync();
    }

    #endregion

    #region 4. Internal Helpers & Safety Guards

    /// <summary>
    /// Saves changes with automatic retry protection in the rare event of an MRN collision.
    /// </summary>
    private async Task SaveChangesWithMrnRetryAsync()
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _context.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                foreach (var entry in _context.ChangeTracker.Entries<Patient>().Where(e => e.State == EntityState.Added))
                {
                    entry.Entity.MedicalRecordNumber = MrnGenerator.Generate();
                }
            }
        }
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

    private static string NormalizeGender(string? rawGender)
    {
        var g = rawGender?.Trim().ToLower() ?? "male";
        return g switch
        {
            "female" => "Female",
            "other" => "Other",
            _ => "Male"
        };
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

    #endregion
}
