namespace VepayDO.Contracts.Receipts;

public sealed record ReceiptResponse(
    Guid Id,
    string Source,
    string ProcessingStatus,
    string ReconciliationStatus,
    string? Risk,
    string Mime,
    int Bytes,
    int? Width,
    int? Height,
    decimal? Amount,
    string? Currency,
    DateOnly? PaymentDate,
    string? Reference,
    string? SenderName,
    string? DestinationLast4,
    Guid? CustomerId,
    Guid? SuggestedChargeId,
    decimal? MatchScore,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ReceiptListRequest(
    string? ProcessingStatus,
    string? ReconciliationStatus,
    string? Risk,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    int? Limit,
    string? Cursor);

public sealed record ReceiptPageResponse(
    IReadOnlyList<ReceiptResponse> Items,
    string? NextCursor);

public sealed record ReceiptUploadResponse(IReadOnlyList<ReceiptResponse> Receipts);

public sealed record ReceiptImageUrlResponse(string Url, DateTimeOffset ExpiresAt);
