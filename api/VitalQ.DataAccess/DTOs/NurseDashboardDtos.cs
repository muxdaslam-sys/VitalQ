using System;
using System.Collections.Generic;

namespace VitalQ.Entities.DTOs;

#region 1. Dashboard Metrics & Doctor Queue Roster

/// <summary>
/// Top-level KPI stats and doctor live queue roster returned for the Nurse Command Center.
/// GET /api/nurse/dashboard/stats
/// </summary>
public class NurseDashboardStatsResponse
{
    public int TotalBookingsToday { get; set; }
    public int TotalTriagedToday { get; set; }
    public int TotalCompletedToday { get; set; }
    public int DoctorsPresent { get; set; }
    public int DoctorsAbsent { get; set; }
    public List<DoctorQueueSummaryDto> Doctors { get; set; } = new();
}

/// <summary>
/// High-level summary of a doctor's live queue status for the doctor roster card grid.
/// </summary>
public class DoctorQueueSummaryDto
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = null!;
    public string Specialization { get; set; } = null!;
    public string RoomNumber { get; set; } = null!;
    public string Status { get; set; } = null!; // "Available", "Busy", "OnLeave"
    public string? CurrentCallingToken { get; set; }
    public int WaitingCount { get; set; }
    public int TotalAssignedToday { get; set; }
}

/// <summary>
/// Detailed live queue returned on-demand when a nurse clicks a doctor card.
/// GET /api/nurse/doctors/{doctorId}/queue
/// </summary>
public class DoctorDetailedQueueResponse
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = null!;
    public string Specialization { get; set; } = null!;
    public string RoomNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public List<DoctorQueueItemDto> Queue { get; set; } = new();
}

/// <summary>
/// An individual patient token inside a doctor's queue with priority and full triage details.
/// </summary>
public class DoctorQueueItemDto
{
    public Guid TokenId { get; set; }
    public string TokenNumber { get; set; } = null!;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string Status { get; set; } = null!; // "Called", "Waiting", "Completed", "Skipped"
    public int PriorityScore { get; set; }
    public string? TriageLevel { get; set; } // "Red", "Yellow", "Green"
    public decimal? SpO2 { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    public decimal? Temperature { get; set; }
    public int? PainScale { get; set; }
    public string? NurseNotes { get; set; }
    public string? NurseName { get; set; }
    public int SkipCount { get; set; }
    public DateTime BookedAtUtc { get; set; }
    public DateTime? TriagedAtUtc { get; set; }
    public DateTime? CalledAtUtc { get; set; }
    public TriageAssessmentResponse? TriageAssessment { get; set; }
}

#endregion

#region 2. Patient Real-Time Queue Position & Visualizer

/// <summary>
/// Detailed queue status and live line visualizer for a specific patient token.
/// GET /api/nurse/tokens/{tokenId}/queue-position
/// </summary>
public class PatientQueuePositionResponse
{
    public Guid TokenId { get; set; }
    public string TokenNumber { get; set; } = null!;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int QueuePosition { get; set; } // 1-based index in the doctor's line
    public int PatientsAhead { get; set; }
    public int EstimatedWaitMinutes { get; set; }
    
    // Doctor & Room
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = null!;
    public string Specialization { get; set; } = null!;
    public string RoomNumber { get; set; } = null!;
    public string? CurrentlyCallingToken { get; set; }
    
    // Triage details if available
    public string? TriageLevel { get; set; }
    public int PriorityScore { get; set; }

    // Live doctor queue list with this patient identified
    public List<DoctorQueueItemDto> DoctorLiveQueue { get; set; } = new();
}

#endregion

#region 3. Emergency Walk-in & Existing Patient Token Booking Payloads

/// <summary>
/// Direct Emergency Walk-in booking payload with Code-Red bypass option.
/// </summary>
public class EmergencyWalkInBookingRequest
{
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = null!;
    public Guid DoctorId { get; set; }
    public bool IsEmergencyCodeRed { get; set; }
    public string? ClinicalNotes { get; set; }
}

/// <summary>
/// Streamlined token booking payload for already registered patients (eliminates re-entering demographics).
/// </summary>
public class ExistingPatientTokenBookingRequest
{
    public Guid DoctorId { get; set; }
    public bool IsEmergencyCodeRed { get; set; }
    public string? ClinicalNotes { get; set; }
}

#endregion

