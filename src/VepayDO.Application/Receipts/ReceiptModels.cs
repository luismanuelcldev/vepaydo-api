using VepayDO.Domain.Enums;
using VepayDO.Domain.Receipts;

namespace VepayDO.Application.Receipts;

public sealed record ReceiptView(
    Guid Id, string Source, string ProcessingStatus, string ReconciliationStatus, string? Risk,
    string Mime, int Bytes, int? Width, int? Height, decimal? Amount, string? Currency,
    DateOnly? PaymentDate, string? Reference, string? SenderName, string? DestinationLast4,
    Guid? CustomerId, Guid? SuggestedChargeId, decimal? MatchScore, string? FailureReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ReceiptRecord(
    Guid Id, Guid IdempotencyKey, Guid UploadedBy, string Source, string StorageKey,
    string Mime, int Bytes, int? Width, int? Height, byte[] Sha256,
    string ProcessingStatus, string ReconciliationStatus, string? Risk,
    decimal? Amount, string? Currency, DateOnly? PaymentDate, string? Reference,
    string? SenderName, string? DestinationLast4, Guid? CustomerId, Guid? SuggestedChargeId,
    decimal? MatchScore, string? FailureReason, DateTimeOffset? ImageDeletedAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ReceiptQuery(
    string? ProcessingStatus, string? ReconciliationStatus, string? Risk,
    DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedTo, int Limit);

public sealed record ReceiptPage(IReadOnlyList<ReceiptView> Items, string? NextCursor);

public sealed record ReceiptJob(Guid Id, Guid TenantId, string Kind, int Attempts, DateTimeOffset AvailableAt);

public sealed record ReceiptUpload(
    IdempotencyKey IdempotencyKey, ReceiptSource Source, string? DeclaredContentType, byte[] Content);

public sealed record ReceiptImageUrl(string Url, DateTimeOffset ExpiresAt);
