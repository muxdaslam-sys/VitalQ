using System;
using System.Collections.Generic;

namespace VitalQ.Entities.Models;

public partial class TokenAuditLog
{
    public Guid Id { get; set; }

    public Guid QueueTokenId { get; set; }

    public string PreviousStatus { get; set; } = null!;

    public string NewStatus { get; set; } = null!;

    public Guid? ChangedByUserId { get; set; }

    public string ChangeSource { get; set; } = null!;

    public string? Notes { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual QueueToken QueueToken { get; set; } = null!;
}
