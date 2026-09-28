using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class Department
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string LocationFloor { get; set; } = null!;

    public int LastTokenNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

    public virtual ICollection<NursingStation> NursingStations { get; set; } = new List<NursingStation>();

    public virtual ICollection<QueueToken> QueueTokens { get; set; } = new List<QueueToken>();
}
