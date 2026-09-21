namespace VitalQ.Entities.DTOs;

// ==========================================
// 1. DATA RETURNED TO CLIENTS (RESPONSES)
// ==========================================

/// <summary>
/// Status transition history record for token audit trail.
/// </summary>
public class TokenAuditLogResponse
{
    public Guid Id { get; set; }
    public Guid QueueTokenId { get; set; }
    public string PreviousStatus { get; set; } = null!;    // e.g. "Booked"
    public string NewStatus { get; set; } = null!;         // e.g. "Triaged"
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public string ChangeSource { get; set; } = null!;      // "User" or "System"
    public string? Notes { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
