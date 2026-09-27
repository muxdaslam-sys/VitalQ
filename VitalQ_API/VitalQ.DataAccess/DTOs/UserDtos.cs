using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Safe user profile returned in responses. Never includes passwords.
/// </summary>
public class UserResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string Role { get; set; } = null!;              // "Admin", "Doctor", "Nurse", "Patient"
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Response returned after successful login or token refresh.
/// </summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public string? RefreshToken { get; set; }
    public UserResponse User { get; set; } = null!;
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Patient registration from mobile phone. (POST /api/auth/register)
/// Creates both a User record and a Patient profile record.
/// </summary>
public class PatientRegisterRequest
{
    [Required, MaxLength(50)]
    public string Username { get; set; } = null!;

    [Required, MinLength(6)]
    public string Password { get; set; } = null!;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(10)]
    public string Gender { get; set; } = null!;            // "Male", "Female", "Other"
}

/// <summary>
/// Login credentials. (POST /api/auth/login)
/// </summary>
public class LoginRequest
{
    [Required]
    public string Username { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}

/// <summary>
/// Admin creates a staff member (Nurse, Doctor, Admin).
/// </summary>
public class CreateUserRequest
{
    [Required, MaxLength(50)]
    public string Username { get; set; } = null!;

    [Required, MinLength(6)]
    public string Password { get; set; } = null!;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Role { get; set; } = null!;              // "Admin", "Doctor", "Nurse"
}

/// <summary>
/// Admin updates a user's details or status.
/// </summary>
public class UpdateUserRequest
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Role { get; set; } = null!;

    public string? Password { get; set; }

    public bool IsActive { get; set; }
}
