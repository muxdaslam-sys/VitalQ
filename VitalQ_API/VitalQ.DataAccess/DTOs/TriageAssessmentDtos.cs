using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Returned when viewing recorded triage vitals and clinical assessment.
/// </summary>
public class TriageAssessmentResponse
{
    public Guid Id { get; set; }
    public Guid QueueTokenId { get; set; }

    // Nurse & Station info
    public Guid NurseUserId { get; set; }
    public string NurseName { get; set; } = string.Empty;
    public Guid? NursingStationId { get; set; }
    public string? StationName { get; set; }

    // Vitals
    public decimal SpO2 { get; set; }                      // Oxygen saturation %
    public int SystolicBp { get; set; }                    // Systolic Blood Pressure
    public int DiastolicBp { get; set; }                   // Diastolic Blood Pressure
    public int HeartRate { get; set; }                     // Heart rate (BPM)
    public decimal Temperature { get; set; }               // Body temperature (°C)
    public int PainScale { get; set; }                     // 0 to 10

    // Computed or overridden triage level
    public string TriageLevel { get; set; } = null!;       // "Red", "Yellow", "Green"
    public bool IsManualOverride { get; set; }             // True if nurse changed the automated score
    public string? OverrideReason { get; set; }
    public string? NurseNotes { get; set; }
    public DateTime AssessedAtUtc { get; set; }
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Submitted by a nurse at the nursing desk when recording patient vitals.
/// POST /api/tokens/{id}/triage
/// </summary>
public class TriageRequest
{
    public Guid? NursingStationId { get; set; }

    [Range(0, 100, ErrorMessage = "SpO2 must be between 0 and 100")]
    public decimal SpO2 { get; set; }

    [Range(0, 300, ErrorMessage = "Systolic BP must be between 0 and 300")]
    public int SystolicBp { get; set; }

    [Range(0, 200, ErrorMessage = "Diastolic BP must be between 0 and 200")]
    public int DiastolicBp { get; set; }

    [Range(0, 300, ErrorMessage = "Heart rate must be between 0 and 300")]
    public int HeartRate { get; set; }

    [Range(30, 45, ErrorMessage = "Temperature must be between 30 and 45 °C")]
    public decimal Temperature { get; set; }

    [Range(0, 10, ErrorMessage = "Pain scale must be between 0 and 10")]
    public int PainScale { get; set; }

    // Clinical override (if nurse feels patient is worse than numbers show)
    public bool IsManualOverride { get; set; } = false;

    [MaxLength(10)]
    public string? OverrideTriageLevel { get; set; }       // "Red", "Yellow", "Green"

    [MaxLength(255)]
    public string? OverrideReason { get; set; }

    public string? NurseNotes { get; set; }
}
