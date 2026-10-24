using VepayDO.Application.Receipts;

namespace VepayDO.Application.Receipts.Ports;

public interface IReceiptRepository
{

    Task<ReceiptRecord?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ReceiptRecord?> FindAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceiptRecord>> ListAsync(
        Guid tenantId,
        ReceiptQuery query,
        ReceiptCursor? cursor,
        CancellationToken cancellationToken = default);

    Task<bool> AddAsync(
        Guid tenantId,
        NewReceipt receipt,
        CancellationToken cancellationToken = default);

    Task MarkProcessingAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken = default);
}

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
