using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Returned when fetching token or queue details.
/// Used by Patient live tracker, Doctor priority queue, and Nursing station.
/// </summary>
public class QueueTokenResponse
{
    public Guid Id { get; set; }
    public string TokenNumber { get; set; } = null!;       // e.g. "CARD-101"
    
    // Patient Info
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;

    // Department & Doctor Info
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty; // e.g. "CARD"
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;

    // Queue & Triage Status
    public string Status { get; set; } = null!;            // Booked, Triaged, Waiting, Called, Skipped, Completed
    public int PriorityScore { get; set; }                 // Calculated priority with aging
    public int BaseWeight { get; set; }                    // Red=100, Yellow=50, Green=10
    public string? TriageLevel { get; set; }               // "Red", "Yellow", "Green"
    public int PatientsAhead { get; set; }                 // Number of patients ahead in line
    public int EstimatedWaitMinutes { get; set; }          // Estimated wait based on doctor consultation time

    // Timestamps
    public DateTime BookedAtUtc { get; set; }
    public DateTime? TriagedAtUtc { get; set; }
    public DateTime? CalledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? ConsultationNotes { get; set; }
    public byte[] RowVersion { get; set; } = null!;        // Concurrency token to prevent duplicate calls
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Used when a patient books an appointment online. (POST /api/bookings)
/// </summary>
public class BookTokenRequest
{
    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }
}

/// <summary>
/// Used by a nurse to create a walk-in token for a patient without an account. (POST /api/tokens/walk-in)
/// </summary>
public class WalkInTokenRequest
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required, MaxLength(10)]
    public string Gender { get; set; } = null!;

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }
}

/// <summary>
/// Used when changing a token status (e.g., doctor skips a patient).
/// </summary>
public class UpdateTokenStatusRequest
{
    [Required, MaxLength(20)]
    public string Status { get; set; } = null!;            // "Skipped", "Cancelled", etc.

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Used when doctor finishes consultation and enters visit notes. (POST /api/tokens/{id}/complete)
/// </summary>
public class CompleteConsultationRequest
{
    [MaxLength(2000)]
    public string? ConsultationNotes { get; set; }
}
