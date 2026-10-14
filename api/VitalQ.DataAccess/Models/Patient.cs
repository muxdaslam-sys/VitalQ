using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class Patient
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string MedicalRecordNumber { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public string Gender { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<QueueToken> QueueTokens { get; set; } = new List<QueueToken>();

    public virtual User? User { get; set; }
}
