using VepayDO.Application.Receipts;

namespace VepayDO.Application.Receipts.Ports;

/// <summary>
/// The store of receipts waiting to be processed. The job id is the receipt id,
/// so enqueuing twice is harmless and a retry is idempotent (M5.4, M5.5).
/// </summary>
public interface IReceiptJobStore
{
    Task EnqueueAsync(Guid receiptId, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Reads the jobs the worker has to process.</summary>
public interface IReceiptJobReader
{
    /// <summary>Claims the next pending jobs, marking them as running.</summary>
    Task<IReadOnlyList<ReceiptJob>> ClaimAsync(int maxJobs, CancellationToken cancellationToken = default);

    /// <summary>Marks a job as finished.</summary>
    Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a job for a later attempt, with an exponential delay. When the
    /// job already used its attempts, it is parked as failed instead of being
    /// retried forever.
    /// </summary>
    Task FailAsync(
        Guid jobId,
        string reason,
        TimeSpan delay,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}
