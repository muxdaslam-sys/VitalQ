using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class Doctor
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public string Specialization { get; set; } = null!;

    public string RoomNumber { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int AvgConsultationMinutes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<QueueToken> QueueTokens { get; set; } = new List<QueueToken>();

    public virtual User User { get; set; } = null!;
}
