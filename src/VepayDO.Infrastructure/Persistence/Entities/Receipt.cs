namespace VepayDO.Infrastructure.Persistence.Entities;

using VepayDO.Domain.Enums;

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
