using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Doctor details returned to patient booking screens and admin portals.
/// </summary>
public class DoctorResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Specialization { get; set; } = null!;
    public string RoomNumber { get; set; } = null!;
    public string Status { get; set; } = null!;            // "Available", "OnLeave", "Busy"
    public int AvgConsultationMinutes { get; set; }        // Feeds patient wait time estimates
    public DateTime CreatedAtUtc { get; set; }
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Used by Admin to register a doctor and their login credentials.
/// </summary>
public class CreateDoctorRequest
{
    // User login fields
    [Required, MaxLength(50)]
    public string Username { get; set; } = null!;

    [Required, MinLength(6)]
    public string Password { get; set; } = null!;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(20), Phone]
    public string PhoneNumber { get; set; } = null!;

    // Doctor profile fields
    [Required]
    public Guid DepartmentId { get; set; }

    [Required, MaxLength(100)]
    public string Specialization { get; set; } = null!;

    [Required, MaxLength(20)]
    public string RoomNumber { get; set; } = null!;

    [Range(1, 180)]
    public int AvgConsultationMinutes { get; set; } = 10;
}

/// <summary>
/// Used by Admin to edit a doctor's info (§07 Admin portal).
/// </summary>
public class UpdateDoctorRequest
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(20), Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required, MaxLength(100)]
    public string Specialization { get; set; } = null!;

    [Required, MaxLength(20)]
    public string RoomNumber { get; set; } = null!;

    [Required, RegularExpression("^(Available|OnLeave|Busy)$", ErrorMessage = "Status must be Available, OnLeave, or Busy")]
    public string Status { get; set; } = "Available";

    [Range(1, 180)]
    public int AvgConsultationMinutes { get; set; } = 10;
}

/// <summary>
/// Quick toggle between Available and OnLeave. (OnLeave hides doctor from booking)
/// </summary>
public class UpdateDoctorStatusRequest
{
    [Required, RegularExpression("^(Available|OnLeave|Busy)$", ErrorMessage = "Status must be Available, OnLeave, or Busy")]
    public string Status { get; set; } = "Available";
}
