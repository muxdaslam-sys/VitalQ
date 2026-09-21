using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Doctor? Doctor { get; set; }

    public virtual Patient? Patient { get; set; }

    public virtual ICollection<TokenAuditLog> TokenAuditLogs { get; set; } = new List<TokenAuditLog>();

    public virtual ICollection<TriageAssessment> TriageAssessments { get; set; } = new List<TriageAssessment>();
}
