namespace VepayDO.Contracts.Receipts;

/// <summary>
/// Wire contracts of the receipts module (UC-RCP-01 to UC-RCP-03). The image
/// itself is never inlined: the client asks for a signed URL (UC-RCP-03).
/// </summary>
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

/// <summary>Query of <c>GET /receipts</c>; every filter is optional.</summary>
public sealed record ReceiptListRequest(
    string? ProcessingStatus,
    string? ReconciliationStatus,
    string? Risk,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    int? Limit,
    string? Cursor);

/// <summary>One page of receipts and the token of the next one.</summary>
public sealed record ReceiptPageResponse(
    IReadOnlyList<ReceiptResponse> Items,
    string? NextCursor);

/// <summary>
/// Result of an upload: one receipt per file, in the order the files were sent
/// (UC-RCP-01).
/// </summary>
public sealed record ReceiptUploadResponse(IReadOnlyList<ReceiptResponse> Receipts);

/// <summary>A time-limited URL that lets the client read the receipt image.</summary>
public sealed record ReceiptImageUrlResponse(string Url, DateTimeOffset ExpiresAt);
