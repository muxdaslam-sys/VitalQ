using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Patient profile information.
/// </summary>
public class PatientResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string MedicalRecordNumber { get; set; } = null!; // e.g. "MRN-2026-0012"
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Returned when nurse searches by phone number or token number. (GET /api/patients/search)
/// </summary>
public class PatientSearchResult
{
    public Guid PatientId { get; set; }
    public string MedicalRecordNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = null!;
    public Guid? ActiveTokenId { get; set; }
    public string? ActiveTokenNumber { get; set; }
    public string? ActiveTokenStatus { get; set; }
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Create patient profile.
/// </summary>
public class CreatePatientRequest
{
    public Guid? UserId { get; set; }

    [MaxLength(30)]
    public string? MedicalRecordNumber { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(10)]
    public string Gender { get; set; } = null!;
}

/// <summary>
/// Update patient profile.
/// </summary>
public class UpdatePatientRequest
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(10)]
    public string Gender { get; set; } = null!;
}
