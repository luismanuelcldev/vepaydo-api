using System.Net;
using VepayDO.Domain.Enums;

namespace VepayDO.Infrastructure.Persistence.Entities;

/// <summary>Uploaded payment receipt. Maps to <c>receipts</c>.</summary>
public sealed class Receipt
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid IdempotencyKey { get; set; }

    public Guid UploadedBy { get; set; }

    public ReceiptSource Source { get; set; }

    public string StorageKey { get; set; } = null!;

    public string Mime { get; set; } = null!;

    public int Bytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public byte[] Sha256 { get; set; } = null!;

    public long? Phash { get; set; }

    public DateTimeOffset? ImageDeletedAt { get; set; }

    public ProcessingStatus ProcessingStatus { get; set; }

    public ReconciliationStatus ReconciliationStatus { get; set; }

    public AlertSeverity? Risk { get; set; }

    public short Attempts { get; set; }

    public string? ProcessingError { get; set; }

    public short? BankId { get; set; }

    public TransferType TransferType { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public DateOnly? PaymentDate { get; set; }

    public TimeOnly? PaymentTime { get; set; }

    public string? Reference { get; set; }

    public string? ReferenceNormalized { get; set; }

    public string? SenderName { get; set; }

    public string? SenderNameNormalized { get; set; }

    public string? DestinationLast4 { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public Guid? CustomerId { get; set; }

    public bool IsFreeIncome { get; set; }

    public Guid? SuggestedChargeId { get; set; }

    public decimal? MatchScore { get; set; }

    public string? MatchSignals { get; set; }

    public Guid? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    public FailureReason? FailureReason { get; set; }

    public RejectReasonCode? RejectReasonCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One OCR/parse run for a receipt. Maps to <c>extractions</c>.</summary>
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

/// <summary>Receipt amount applied to a charge. Maps to <c>allocations</c>.</summary>
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

/// <summary>Alert raised for a receipt. Maps to <c>alerts</c>.</summary>
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

/// <summary>Append-only audit trail entry. Maps to <c>audit_events</c>.</summary>
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
