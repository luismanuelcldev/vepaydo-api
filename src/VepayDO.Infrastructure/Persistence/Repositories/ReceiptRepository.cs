using Microsoft.EntityFrameworkCore;

using VepayDO.Application.Receipts;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes the current tenant's <c>receipts</c> rows. The table carries
/// the <c>tenant_isolation</c> RLS policy, so every query is confined to
/// <c>app.tenant_id</c> on top of the explicit filter (AGENTS.md section 7).
/// </summary>
public sealed class ReceiptRepository(TxProvider tx, IClock clock) : IReceiptRepository
{
    public async Task<ReceiptRecord?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await tx.DbContext.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<ReceiptRecord?> FindAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var entity = await tx.DbContext.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<ReceiptRecord>> ListAsync(
        Guid tenantId,
        ReceiptQuery query,
        ReceiptCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The page is read with a keyset pagination: the row comparison
        // (created_at, id) is what PostgreSQL compares, so the page never skips
        // nor repeats a receipt that shares a timestamp with another one.
        var rows = await tx.DbContext.Receipts
            .FromSqlInterpolated(
                $"""
                 SELECT r.* FROM receipts r
                  WHERE r.tenant_id = {tenantId}
                    AND ({query.ProcessingStatus} IS NULL OR r.processing_status::text = {query.ProcessingStatus})
                    AND ({query.ReconciliationStatus} IS NULL OR r.reconciliation_status::text = {query.ReconciliationStatus})
                    AND ({query.Risk} IS NULL OR r.risk::text = {query.Risk})
                    AND ({query.CreatedFrom} IS NULL OR r.created_at >= {query.CreatedFrom})
                    AND ({query.CreatedTo} IS NULL OR r.created_at <= {query.CreatedTo})
                    AND ({cursor?.CreatedAt} IS NULL OR (r.created_at, r.id) < ({cursor?.CreatedAt}, {cursor?.Id}))
                  ORDER BY r.created_at DESC, r.id DESC
                  LIMIT {query.Limit}
                 """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<bool> AddAsync(
        Guid tenantId,
        NewReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var now = clock.UtcNow;

        // ON CONFLICT DO NOTHING keeps a concurrent upload with the same
        // idempotency key harmless: the statement reports zero affected rows
        // instead of aborting the transaction with a unique violation.
        var affected = await tx.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO receipts (
                 id, tenant_id, idempotency_key, uploaded_by, source, storage_key, mime, bytes,
                 width, height, sha256, processing_status, reconciliation_status, transfer_type,
                 is_free_income, created_at, updated_at)
             VALUES (
                 {receipt.Id}, {tenantId}, {receipt.IdempotencyKey}, {receipt.UploadedBy},
                 CAST({receipt.Source} AS receipt_source), {receipt.StorageKey}, {receipt.Mime},
                 {receipt.Bytes}, {receipt.Width}, {receipt.Height}, {receipt.Sha256},
                 CAST('RECEIVED' AS processing_status), CAST('PENDING_REVIEW' AS reconciliation_status),
                 CAST('UNKNOWN' AS transfer_type), false, {now}, {now})
             ON CONFLICT (tenant_id, idempotency_key) DO NOTHING
             """,
            cancellationToken).ConfigureAwait(false);

        return affected > 0;
    }

    public async Task MarkProcessingAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        await tx.DbContext.Receipts
            .Where(receipt => receipt.TenantId == tenantId && receipt.Id == receiptId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(receipt => receipt.ProcessingStatus, ProcessingStatus.PROCESSING)
                    .SetProperty(receipt => receipt.UpdatedAt, clock.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ReceiptRecord ToRecord(Receipt entity) =>
        new(
            entity.Id,
            entity.IdempotencyKey,
            entity.UploadedBy,
            entity.Source.ToString(),
            entity.StorageKey,
            entity.Mime,
            entity.Bytes,
            entity.Width,
            entity.Height,
            entity.Sha256,
            entity.ProcessingStatus.ToString(),
            entity.ReconciliationStatus.ToString(),
            entity.Risk?.ToString(),
            entity.Amount,
            entity.Currency,
            entity.PaymentDate,
            entity.Reference,
            entity.SenderName,
            entity.DestinationLast4,
            entity.CustomerId,
            entity.SuggestedChargeId,
            entity.MatchScore,
            entity.FailureReason?.ToString(),
            entity.ImageDeletedAt,
            entity.CreatedAt,
            entity.UpdatedAt);
}
