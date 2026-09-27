using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class TriageAssessment
{
    public Guid Id { get; set; }

    public Guid QueueTokenId { get; set; }

    public Guid NurseUserId { get; set; }

    public Guid? NursingStationId { get; set; }

    public string TriageLevel { get; set; } = null!;

    public decimal SpO2 { get; set; }

    public int SystolicBp { get; set; }

    public int DiastolicBp { get; set; }

    public int HeartRate { get; set; }

    public decimal Temperature { get; set; }

    public int PainScale { get; set; }

    public bool IsManualOverride { get; set; }

    public string? OverrideReason { get; set; }

    public string? NurseNotes { get; set; }

    public DateTime AssessedAtUtc { get; set; }

    public virtual User NurseUser { get; set; } = null!;

    public virtual NursingStation? NursingStation { get; set; }

    public virtual QueueToken QueueToken { get; set; } = null!;
}
