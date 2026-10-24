using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Receipts;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class ReceiptRepository(TxProvider tx, IClock clock) : IReceiptRepository
{
    public async Task<ReceiptRecord?> FindByIdempotencyKeyAsync(Guid tenantId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        var entity = await tx.DbContext.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IdempotencyKey == idempotencyKey, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<ReceiptRecord?> FindAsync(Guid tenantId, Guid receiptId, CancellationToken cancellationToken = default)
    {
        var entity = await tx.DbContext.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == receiptId, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<ReceiptRecord>> ListAsync(Guid tenantId, ReceiptQuery query, ReceiptCursor? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = await tx.DbContext.Receipts.FromSqlInterpolated($"""
            SELECT r.* FROM receipts r WHERE r.tenant_id = {tenantId}
              AND ({query.ProcessingStatus} IS NULL OR r.processing_status::text = {query.ProcessingStatus})
              AND ({query.ReconciliationStatus} IS NULL OR r.reconciliation_status::text = {query.ReconciliationStatus})
              AND ({query.Risk} IS NULL OR r.risk::text = {query.Risk})
              AND ({query.CreatedFrom} IS NULL OR r.created_at >= {query.CreatedFrom})
              AND ({query.CreatedTo} IS NULL OR r.created_at <= {query.CreatedTo})
              AND ({cursor?.CreatedAt} IS NULL OR (r.created_at, r.id) < ({cursor?.CreatedAt}, {cursor?.Id}))
            ORDER BY r.created_at DESC, r.id DESC LIMIT {query.Limit}
            """).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<bool> AddAsync(Guid tenantId, NewReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var now = clock.UtcNow;
        var affected = await tx.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO receipts (id, tenant_id, idempotency_key, uploaded_by, source, storage_key, mime, bytes, width, height, sha256, processing_status, reconciliation_status, transfer_type, is_free_income, created_at, updated_at)
            VALUES ({receipt.Id}, {tenantId}, {receipt.IdempotencyKey}, {receipt.UploadedBy}, CAST({receipt.Source} AS receipt_source), {receipt.StorageKey}, {receipt.Mime}, {receipt.Bytes}, {receipt.Width}, {receipt.Height}, {receipt.Sha256}, CAST('RECEIVED' AS processing_status), CAST('PENDING_REVIEW' AS reconciliation_status), CAST('UNKNOWN' AS transfer_type), false, {now}, {now})
            ON CONFLICT (tenant_id, idempotency_key) DO NOTHING
            """, cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task MarkProcessingAsync(Guid tenantId, Guid receiptId, CancellationToken cancellationToken = default)
    {
        await tx.DbContext.Receipts.Where(r => r.TenantId == tenantId && r.Id == receiptId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ProcessingStatus, ProcessingStatus.PROCESSING).SetProperty(r => r.UpdatedAt, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    private static ReceiptRecord ToRecord(Receipt e) => new(
        e.Id, e.IdempotencyKey, e.UploadedBy, e.Source.ToString(), e.StorageKey, e.Mime, e.Bytes, e.Width, e.Height, e.Sha256,
        e.ProcessingStatus.ToString(), e.ReconciliationStatus.ToString(), e.Risk?.ToString(), e.Amount, e.Currency, e.PaymentDate,
        e.Reference, e.SenderName, e.DestinationLast4, e.CustomerId, e.SuggestedChargeId, e.MatchScore, e.FailureReason?.ToString(),
        e.ImageDeletedAt, e.CreatedAt, e.UpdatedAt);
}
