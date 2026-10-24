using Microsoft.EntityFrameworkCore;

using VepayDO.Application.Receipts;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// The processing queue of the backend, backed by <c>processing_jobs</c>.
///
/// <para>
/// The job id is the receipt id, so enqueuing twice is harmless and a retry is
/// idempotent by job id (M5.4, M5.5). The table carries no business data: only
/// identifiers and state, so the worker can discover the jobs of every tenant
/// without a tenant context.
/// </para>
/// </summary>
public sealed class ReceiptJobStore(
    TxProvider tx,
    AppDbContext db,
    IClock clock) : IReceiptJobStore, IReceiptJobReader
{
    private const string Pending = "PENDING";
    private const string Running = "RUNNING";
    private const string Done = "DONE";
    private const string Failed = "FAILED";
    private const string ReceiptProcessing = "RECEIPT_PROCESSING";

    public async Task EnqueueAsync(
        Guid receiptId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // ON CONFLICT DO NOTHING: the job id is the receipt id, so a receipt
        // that is already queued is not an error.
        await tx.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO processing_jobs (
                 id, tenant_id, kind, status, attempts, available_at, created_at, updated_at)
             VALUES (
                 {receiptId}, {tenantId}, {ReceiptProcessing}, {Pending}, 0, {now}, {now}, {now})
             ON CONFLICT (id) DO NOTHING
             """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReceiptJob>> ClaimAsync(
        int maxJobs,
        CancellationToken cancellationToken = default)
    {
        if (maxJobs <= 0)
        {
            return [];
        }

        var claimed = await db.Database
            .SqlQuery<ClaimedJob>($"""
                WITH claimed AS (
                    UPDATE processing_jobs
                       SET status = 'RUNNING',
                           attempts = attempts + 1,
                           updated_at = now()
                     WHERE id IN (
                           SELECT id
                             FROM processing_jobs
                            WHERE status = 'PENDING'
                              AND available_at <= now()
                            ORDER BY available_at, created_at
                            FOR UPDATE SKIP LOCKED
                            LIMIT {maxJobs})
                    RETURNING id, tenant_id, kind, attempts, available_at
                )
                SELECT id AS "Id",
                       tenant_id AS "TenantId",
                       kind AS "Kind",
                       attempts AS "Attempts",
                       available_at AS "AvailableAt"
                  FROM claimed
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed
            .Select(job => new ReceiptJob(job.Id, job.TenantId, job.Kind, job.Attempts, job.AvailableAt))
            .ToList();
    }

    public async Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        await db.ProcessingJobs
            .Where(job => job.Id == jobId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, Done)
                    .SetProperty(job => job.LastError, (string?)null)
                    .SetProperty(job => job.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task FailAsync(
        Guid jobId,
        string reason,
        TimeSpan delay,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var job = await db.ProcessingJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            return;
        }

        // A job that used all its attempts is parked: it stays visible for the
        // operator instead of being retried forever.
        var exhausted = job.Attempts >= maxAttempts;
        var status = exhausted ? Failed : Pending;

        await db.ProcessingJobs
            .Where(candidate => candidate.Id == jobId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, status)
                    .SetProperty(candidate => candidate.LastError, reason)
                    .SetProperty(candidate => candidate.AvailableAt, now.Add(delay))
                    .SetProperty(candidate => candidate.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
