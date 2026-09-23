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

/// <summary>
/// Full patient profile returned in the Admin Patient Directory (§07 Admin portal).
/// Includes identity details and all past queue tokens/visits.
/// </summary>
public class PatientDetailResponse
{
    public Guid Id { get; set; }
    public string MedicalRecordNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = null!;
    public bool IsRegisteredAppUser { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<PatientVisitHistoryDto> Visits { get; set; } = new();
}

/// <summary>
/// Summary of an individual consultation/visit.
/// </summary>
public class PatientVisitHistoryDto
{
    public Guid TokenId { get; set; }
    public string TokenNumber { get; set; } = null!;
    public string DepartmentName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string Status { get; set; } = null!;
    public string? TriageLevel { get; set; }
    public DateTime BookedAtUtc { get; set; }
    public DateTime? TriagedAtUtc { get; set; }
    public DateTime? CalledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ConsultationNotes { get; set; }
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
