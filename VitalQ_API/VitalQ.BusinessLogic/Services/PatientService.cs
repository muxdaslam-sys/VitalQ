using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
        var phone = request.PhoneNumber.Trim();

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

        // Step 2: Fetch all patient records sharing this family phone number
        return await _context.Patients
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
        var phone = request.PhoneNumber.Trim();
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

        var q = query.Trim().ToLower();

        return await _context.Patients
            .Where(p => p.PhoneNumber.Contains(q) ||
                        p.FullName.ToLower().Contains(q) ||
                        p.MedicalRecordNumber.ToLower().Contains(q))
            .Take(10)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    // Generates a hospital Medical Record Number like "MRN-20260927-4821"
    private static string GenerateMrn() =>
        $"MRN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

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
