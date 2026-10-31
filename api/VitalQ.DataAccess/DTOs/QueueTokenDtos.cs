using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

#region 1. Data Returned to Clients (Responses)

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
    public int BaseWeight { get; set; }                    // Red=1000, Yellow=500, Green=100
    public string? TriageLevel { get; set; }               // "Red", "Yellow", "Green"
    public int PatientsAhead { get; set; }                 // Number of patients ahead in line
    public int EstimatedWaitMinutes { get; set; }          // Estimated wait based on doctor consultation time

    // Embedded Clinical Triage Vitals (Zero-latency instant preview)
    public decimal? SpO2 { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    public decimal? Temperature { get; set; }
    public int? PainScale { get; set; }
    public string? NurseNotes { get; set; }
    public string? NurseName { get; set; }

    /// <summary>
    /// Cumulative number of times this token has been skipped due to absence (0, 1, 2, or 3).
    /// Used by Doctor, Nurse, and Patient screens to display strike warnings (Call X of 3).
    /// </summary>
    public int SkipCount { get; set; }

    // Nursing Station & Wayfinding Navigation
    public Guid? NursingStationId { get; set; }
    public string? NursingStationName { get; set; }        // e.g. "Desk A - Cardiology"
    public string? LocationFloor { get; set; }              // e.g. "Floor 1"
    public string? NextStepInstruction { get; set; }        // e.g. "Please report to Desk A - Cardiology (Floor 1) for Vitals"

    // Timestamps
    public DateTime BookedAtUtc { get; set; }
    public DateTime? TriagedAtUtc { get; set; }
    public DateTime? CalledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? ConsultationNotes { get; set; }
    public byte[] RowVersion { get; set; } = null!;        // Concurrency token to prevent duplicate calls
}

#endregion

#region 2. Data Sent from Clients (Requests)

/// <summary>
/// Used when a patient books an appointment online. (POST /api/bookings)
/// </summary>
public class BookTokenRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }
}

/// <summary>
/// Optional payload when cancelling an appointment.
/// </summary>
public class CancelTokenRequest
{
    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Used when doctor finishes consultation and enters visit notes. (POST /api/tokens/{id}/complete)
/// </summary>
public class CompleteConsultationRequest
{
    [Required(ErrorMessage = "Clinical consultation notes, diagnosis, or prescription advice are mandatory for patient discharge.")]
    [MinLength(5, ErrorMessage = "Consultation notes must be at least 5 characters long.")]
    [MaxLength(2000, ErrorMessage = "Consultation notes cannot exceed 2000 characters.")]
    public string ConsultationNotes { get; set; } = null!;
}

#endregion
