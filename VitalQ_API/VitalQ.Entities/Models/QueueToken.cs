using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class QueueToken
{
    public Guid Id { get; set; }

    public string TokenNumber { get; set; } = null!;

    public Guid PatientId { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid DoctorId { get; set; }

    public string Status { get; set; } = null!;

    public int PriorityScore { get; set; }

    public int BaseWeight { get; set; }

    public DateTime BookedAtUtc { get; set; }

    public DateTime? TriagedAtUtc { get; set; }

    public DateTime? CalledAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? ConsultationNotes { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;

    public virtual ICollection<TokenAuditLog> TokenAuditLogs { get; set; } = new List<TokenAuditLog>();

    public virtual TriageAssessment? TriageAssessment { get; set; }
}
