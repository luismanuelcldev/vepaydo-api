using VepayDO.Application.Receipts;

namespace VepayDO.Application.Receipts.Ports;

public interface IReceiptJobStore
{
    Task EnqueueAsync(Guid receiptId, Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IReceiptJobReader
{

    Task<IReadOnlyList<ReceiptJob>> ClaimAsync(int maxJobs, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task FailAsync(
        Guid jobId,
        string reason,
        TimeSpan delay,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}
