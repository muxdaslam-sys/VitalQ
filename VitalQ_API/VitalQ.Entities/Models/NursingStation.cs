using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class NursingStation
{
    public Guid Id { get; set; }

    public string StationName { get; set; } = null!;

    public Guid? DepartmentId { get; set; }

    public string LocationFloor { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Department? Department { get; set; }

    public virtual ICollection<TriageAssessment> TriageAssessments { get; set; } = new List<TriageAssessment>();
}
