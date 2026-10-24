using VepayDO.Application.Receipts;

namespace VepayDO.Application.Receipts.Ports;

/// <summary>
/// Persistence port for receipts. Every method is tenant-scoped: the
/// implementation filters by <c>tenant_id</c> and the RLS policy of the table
/// confines it again (AGENTS.md section 7).
/// </summary>
public interface IReceiptRepository
{
    /// <summary>Finds the receipt created with the idempotency key, if any.</summary>
    Task<ReceiptRecord?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a receipt by id.</summary>
    Task<ReceiptRecord?> FindAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of receipts ordered by creation date (newest first). When
    /// a cursor is given, only the receipts older than it are returned.
    /// </summary>
    Task<IReadOnlyList<ReceiptRecord>> ListAsync(
        Guid tenantId,
        ReceiptQuery query,
        ReceiptCursor? cursor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a receipt. Returns <c>false</c> when a receipt with the same
    /// idempotency key already exists: the insert is skipped instead of failing,
    /// so a concurrent upload of the same file never breaks the transaction.
    /// </summary>
    Task<bool> AddAsync(
        Guid tenantId,
        NewReceipt receipt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the receipt to <c>PROCESSING</c>: the worker picked it up and the
    /// fields are waiting for the extraction (or for a human, while the OCR
    /// engine is undecided).
    /// </summary>
    Task MarkProcessingAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken = default);
}

/// <summary>The values of a receipt about to be stored.</summary>
public sealed record NewReceipt(
    Guid Id,
    Guid IdempotencyKey,
    Guid UploadedBy,
    string Source,
    string StorageKey,
    string Mime,
    int Bytes,
    int? Width,
    int? Height,
    byte[] Sha256);
