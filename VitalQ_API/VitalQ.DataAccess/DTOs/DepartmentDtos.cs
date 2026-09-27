using System.ComponentModel.DataAnnotations;

namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Department details returned to booking and admin screens.
/// </summary>
public class DepartmentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;              // e.g. "Cardiology"
    public string Code { get; set; } = null!;              // e.g. "CARD" (used as token prefix)
    public string LocationFloor { get; set; } = null!;
    public int LastTokenNumber { get; set; }               // Current token sequence number
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// ==========================================
// 2. DATA SENT FROM CLIENTS (REQUESTS)
// ==========================================

/// <summary>
/// Admin adds a new clinical department.
/// </summary>
public class CreateDepartmentRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(10)]
    public string Code { get; set; } = null!;              // e.g. "CARD", "ORTH"

    [Required, MaxLength(20)]
    public string LocationFloor { get; set; } = null!;
}

/// <summary>
/// Admin updates an existing department.
/// </summary>
public class UpdateDepartmentRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(20)]
    public string LocationFloor { get; set; } = null!;

    public bool IsActive { get; set; }
}
