namespace VepayDO.Infrastructure.Persistence.Entities;

using System.Net;
using VepayDO.Domain.Enums;

public sealed class Extraction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReceiptId { get; set; }
    public string Engine { get; set; } = null!;
    public string? Parser { get; set; }
    public short? DetectedBankId { get; set; }
    public string Fields { get; set; } = null!;
    public decimal? MinConfidence { get; set; }
    public string? Raw { get; set; }
    public DateTimeOffset? RawDeletedAt { get; set; }
    public int? DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Allocation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid ChargeId { get; set; }
    public decimal AmountApplied { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}

public sealed class Alert
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReceiptId { get; set; }
    public AlertRuleCode Rule { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = null!;
    public string Details { get; set; } = null!;
    public AlertStatus Status { get; set; }
    public AlertResolution? Resolution { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public AuditActor Actor { get; set; }
    public string Entity { get; set; } = null!;
    public Guid? EntityId { get; set; }
    public string Action { get; set; } = null!;
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Reason { get; set; }
    public IPAddress? Ip { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
