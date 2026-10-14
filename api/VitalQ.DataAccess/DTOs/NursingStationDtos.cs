using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Nursing station desk information.
/// </summary>
public class NursingStationResponse
{
    public Guid Id { get; set; }
    public string StationName { get; set; } = null!;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string LocationFloor { get; set; } = null!;
    public bool IsActive { get; set; }
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Admin defines a new nursing desk.
/// </summary>
public class CreateNursingStationRequest
{
    [Required, MaxLength(100)]
    public string StationName { get; set; } = null!;

    public Guid? DepartmentId { get; set; }

    [Required, MaxLength(20)]
    public string LocationFloor { get; set; } = null!;
}

/// <summary>
/// Admin updates an existing nursing desk.
/// </summary>
public class UpdateNursingStationRequest
{
    [Required, MaxLength(100)]
    public string StationName { get; set; } = null!;

    public Guid? DepartmentId { get; set; }

    [Required, MaxLength(20)]
    public string LocationFloor { get; set; } = null!;

    public bool IsActive { get; set; }
}
