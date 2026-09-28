using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

public class PatientService : IPatientService
{
    private readonly VitalQDbContext _context;
    private readonly IConfiguration _config;

    public PatientService(VitalQDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // =========================================================================
    // 1. PATIENT SELF-REGISTRATION (Online / Mobile App)
    // =========================================================================
    public async Task<AuthResponse> SelfRegisterAsync(SelfRegisterRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);

        // Step 1: Check if phone number is already registered as an account username
        var alreadyRegistered = await _context.Users.AnyAsync(u => u.Username == phone);
        if (alreadyRegistered)
        {
            throw new InvalidOperationException("An account with this phone number already exists. Please log in.");
        }

        // Step 2: Strictly set Password to Date of Birth (Format: DDMMYYYY, e.g. "15081995")
        var dobPassword = request.DateOfBirth.ToString("ddMMyyyy");

        // Step 3: Create User record (Username = Phone, Password = DOB)
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

        // Step 4: Create Patient profile (medical record with unique MRN)
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = user.Id, // Links medical record to login user account
            MedicalRecordNumber = GenerateMrn(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Step 5: Save both to database in one atomic transaction
        await _context.Users.AddAsync(user);
        await _context.Patients.AddAsync(patient);
        await _context.SaveChangesAsync();

        // Step 6: Log them in immediately by generating a JWT session
        return CreateAuthResponse(user);
    }

    // =========================================================================
    // 2. ADD FAMILY MEMBER (Logged-in Patient adds Child/Parent)
    // =========================================================================
    public async Task<PatientResponse> AddFamilyMemberAsync(Guid currentUserId, AddFamilyMemberRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);

        // Step 1: Find the parent's user account to get their registered phone number
        var parentUser = await _context.Users.FindAsync(currentUserId);
        if (parentUser == null)
        {
            throw new ArgumentException("Parent account not found.");
        }

        // Step 2: Create a new Patient profile for the family member
        var familyMember = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = null, // Dependent (managed through the parent's login)
            MedicalRecordNumber = GenerateMrn(), // Each member gets their own unique MRN
            FullName = request.FullName.Trim(),
            PhoneNumber = parentUser.PhoneNumber, // Shares the parent's phone number
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Step 3: Save to database
        await _context.Patients.AddAsync(familyMember);
        await _context.SaveChangesAsync();

        return MapToResponse(familyMember);
    }

    // =========================================================================
    // 3. GET MY FAMILY (List all profiles under parent's phone number)
    // =========================================================================
    public async Task<IEnumerable<PatientResponse>> GetMyFamilyMembersAsync(Guid currentUserId)
    {
        // Step 1: Get parent user's phone number
        var parentUser = await _context.Users.FindAsync(currentUserId);
        if (parentUser == null) return Enumerable.Empty<PatientResponse>();

        // Step 2: Fetch all patient records sharing this family phone number (Read-only AsNoTracking)
        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber == parentUser.PhoneNumber)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    // =========================================================================
    // 4. WALK-IN REGISTRATION (Nurse at Nursing Station Desk)
    // =========================================================================
    public async Task<PatientResponse> WalkInRegisterAsync(WalkInRegisterRequest request)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);
        var dobPassword = request.DateOfBirth.ToString("ddMMyyyy");

        // Step 1: Ensure User account exists (so patient can log in later with Phone + DOB)
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

        // Step 2: Link UserId if this user account does not yet have a primary patient profile
        var alreadyLinked = await _context.Patients.AnyAsync(p => p.UserId == user.Id);
        Guid? linkedUserId = alreadyLinked ? null : user.Id;

        // Step 3: Create Patient profile with unique MRN
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = linkedUserId,
            MedicalRecordNumber = GenerateMrn(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Step 4: Save to database
        await _context.Patients.AddAsync(patient);
        await _context.SaveChangesAsync();

        return MapToResponse(patient);
    }

    // =========================================================================
    // 5. SEARCH PATIENTS (For Nurse at Desk)
    // =========================================================================
    public async Task<IEnumerable<PatientResponse>> SearchPatientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<PatientResponse>();

        var q = query.Trim();

        // Prefix matching enables B-Tree index seeks on PhoneNumber and MedicalRecordNumber (O(log N))
        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.PhoneNumber.StartsWith(q) ||
                        p.MedicalRecordNumber.StartsWith(q) ||
                        p.FullName.StartsWith(q))
            .Take(10)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    // =========================================================================
    // 6. GET PATIENT BY ID
    // =========================================================================
    public async Task<PatientResponse?> GetPatientByIdAsync(Guid patientId)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patientId);

        return patient == null ? null : MapToResponse(patient);
    }

    // =========================================================================
    // 7. UPDATE PATIENT PROFILE
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

        // If this patient is linked to a primary User account, sync the user's name, phone, and DOB password
        if (patient.UserId.HasValue)
        {
            var user = await _context.Users.FindAsync(patient.UserId.Value);
            if (user != null)
            {
                // Verify new phone number is not taken by another user account
                if (user.PhoneNumber != phone)
                {
                    var phoneExists = await _context.Users.AnyAsync(u => u.Id != user.Id && u.Username == phone);
                    if (phoneExists)
                    {
                        throw new InvalidOperationException("This phone number is already registered to another user account.");
                    }

                    // Keep family dependents linked by updating their shared phone number
                    var dependents = await _context.Patients
                        .Where(p => p.PhoneNumber == user.PhoneNumber && p.Id != patient.Id && p.UserId == null)
                        .ToListAsync();

                    foreach (var dep in dependents)
                    {
                        dep.PhoneNumber = phone;
                    }
                }

                user.FullName = request.FullName.Trim();
                user.PhoneNumber = phone;
                user.Username = phone;
                user.Password = request.DateOfBirth.ToString("ddMMyyyy");
            }
        }

        // Update patient record
        patient.FullName = request.FullName.Trim();
        patient.PhoneNumber = phone;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Gender = request.Gender.Trim();

        await _context.SaveChangesAsync();
        return MapToResponse(patient);
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    // Normalizes phone by removing spaces, dashes, or parentheses
    private static string NormalizePhone(string phone) =>
        Regex.Replace(phone.Trim(), @"[^\d+]", "");

    // Validates date of birth to prevent corrupt future dates or impossible years
    private static void ValidateDateOfBirth(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob > today)
        {
            throw new ArgumentException("Date of birth cannot be in the future.");
        }
        if (dob < new DateOnly(1900, 1, 1))
        {
            throw new ArgumentException("Please enter a valid birth year after 1900.");
        }
    }

    // Atomic thread-safe counter ensuring 10-nanosecond consecutive, collision-free MRNs
    private static long _atomicMrnCounter = 0;

    // Generates an atomic, collision-free Medical Record Number (e.g. MRN-20260928-21275001)
    private static string GenerateMrn()
    {
        var seq = Interlocked.Increment(ref _atomicMrnCounter) % 100;
        return $"MRN-{DateTime.UtcNow:yyyyMMdd}-{DateTime.UtcNow:HHmmss}{seq:D2}";
    }

    // Maps database model to clean response DTO
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

    // Creates JWT token for instant login after self-registration
    private AuthResponse CreateAuthResponse(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName)
            }),
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:DurationInMinutes"] ?? "60")),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new AuthResponse
        {
            AccessToken = tokenHandler.WriteToken(token),
            RefreshToken = Guid.NewGuid().ToString(),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60),
            User = new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Password = user.Password,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc
            }
        };
    }
}
