using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Receipts;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class ReceiptJobStore(TxProvider tx, AppDbContext db, IClock clock) : IReceiptJobStore, IReceiptJobReader
{
    private const string Pending = "PENDING";
    private const string Running = "RUNNING";
    private const string Done = "DONE";
    private const string Failed = "FAILED";
    private const string ReceiptProcessing = "RECEIPT_PROCESSING";

    public async Task EnqueueAsync(Guid receiptId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        await tx.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO processing_jobs (id, tenant_id, kind, status, attempts, available_at, created_at, updated_at) VALUES ({receiptId}, {tenantId}, {ReceiptProcessing}, {Pending}, 0, {now}, {now}, {now}) ON CONFLICT (id) DO NOTHING",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReceiptJob>> ClaimAsync(int maxJobs, CancellationToken cancellationToken = default)
    {
        if (maxJobs <= 0) return [];
        var claimed = await db.Database.SqlQuery<ClaimedJob>($"""
            WITH claimed AS (
                UPDATE processing_jobs SET status = 'RUNNING', attempts = attempts + 1, updated_at = now()
                 WHERE id IN (SELECT id FROM processing_jobs WHERE status = 'PENDING' AND available_at <= now() ORDER BY available_at, created_at FOR UPDATE SKIP LOCKED LIMIT {maxJobs})
                RETURNING id, tenant_id, kind, attempts, available_at
            )
            SELECT id AS "Id", tenant_id AS "TenantId", kind AS "Kind", attempts AS "Attempts", available_at AS "AvailableAt" FROM claimed
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

        return claimed.Select(job => new ReceiptJob(job.Id, job.TenantId, job.Kind, job.Attempts, job.AvailableAt)).ToList();
    }

    public async Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        await db.ProcessingJobs.Where(job => job.Id == jobId).ExecuteUpdateAsync(
            s => s.SetProperty(j => j.Status, Done).SetProperty(j => j.LastError, (string?)null).SetProperty(j => j.UpdatedAt, now),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task FailAsync(Guid jobId, string reason, TimeSpan delay, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var job = await db.ProcessingJobs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == jobId, cancellationToken).ConfigureAwait(false);
        if (job is null) return;

        var exhausted = job.Attempts >= maxAttempts;
        var status = exhausted ? Failed : Pending;

        await db.ProcessingJobs.Where(c => c.Id == jobId).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.Status, status).SetProperty(c => c.LastError, reason).SetProperty(c => c.AvailableAt, now.Add(delay)).SetProperty(c => c.UpdatedAt, now),
            cancellationToken).ConfigureAwait(false);
    }
}
