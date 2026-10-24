using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Application.Storage;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Receipts;

public sealed partial class ReceiptService(
    IReceiptRepository receipts,
    IReceiptImageProcessor images,
    IStorageToolkit storage,
    IReceiptJobStore queue,
    IAuditWriter audit,
    IClock clock,
    ReceiptOptions options,
    ITenantContextAccessor tenantContext) : IReceiptService
{
    public async Task<ReceiptPage> ListAsync(ReceiptQuery query, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tenantId = RequireTenantId();

        var normalized = query with
        {
            ProcessingStatus = Normalize<ProcessingStatus>(query.ProcessingStatus, nameof(query.ProcessingStatus)),
            ReconciliationStatus = Normalize<ReconciliationStatus>(query.ReconciliationStatus, nameof(query.ReconciliationStatus)),
            Risk = Normalize<AlertSeverity>(query.Risk, nameof(query.Risk)),
        };

        var parsedCursor = cursor is null ? null : ReceiptCursor.Decode(cursor);
        var limit = Math.Clamp(query.Limit, 1, 100);
        var rows = await receipts.ListAsync(tenantId, normalized, parsedCursor, cancellationToken).ConfigureAwait(false);

        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToList();
        string? nextCursor = hasMore && page.Count > 0 ? new ReceiptCursor(page[^1].CreatedAt, page[^1].Id).Encode() : null;

        return new ReceiptPage(page.Select(ToView).ToList(), nextCursor);
    }

    public async Task<ReceiptView> GetAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var record = await receipts.FindAsync(tenantId, receiptId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Receipt not found");
        return ToView(record);
    }

    public async Task<ReceiptImageUrl> GetImageUrlAsync(Guid receiptId, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);
        var tenantId = RequireTenantId();
        var record = await receipts.FindAsync(tenantId, receiptId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Receipt not found");

        if (record.ImageDeletedAt is not null) throw new NotFoundException("The receipt image is no longer available");

        var lifetime = TimeSpan.FromSeconds(Math.Max(30, options.SignedUrlTtlSeconds));
        var url = await storage.SignedUrls.CreateSignedUrlAsync(record.StorageKey, lifetime, cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(tenantId, receiptId, "image.access", before: null, after: new { expiresInSeconds = (int)lifetime.TotalSeconds }, meta, cancellationToken).ConfigureAwait(false);
        return new ReceiptImageUrl(url, clock.UtcNow.Add(lifetime));
    }

    private Task WriteAuditAsync(Guid tenantId, Guid entityId, string action, object? before, object? after, RequestMeta meta, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(tenantContext.Current?.UserId).Entity("receipt").EntityId(entityId).Action(action).Before(before).After(after).Ip(meta.Ip).Build(), cancellationToken);

    private static string? Normalize<TEnum>(string? value, string label) where TEnum : struct, System.Enum
    {
        if (value is null) return null;
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new InvalidArgumentDomainException($"{label} must be one of {string.Join(", ", Enum.GetNames<TEnum>())}");
        return parsed.ToString();
    }

    private static string HashOf(byte[] content) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

    private static ReceiptView ToView(ReceiptRecord record) =>
        new(record.Id, record.Source, record.ProcessingStatus, record.ReconciliationStatus, record.Risk, record.Mime, record.Bytes, record.Width, record.Height, record.Amount, record.Currency, record.PaymentDate, record.Reference, record.SenderName, record.DestinationLast4, record.CustomerId, record.SuggestedChargeId, record.MatchScore, record.FailureReason, record.CreatedAt, record.UpdatedAt);

    private Guid RequireTenantId() => tenantContext.Current?.TenantId is { } id && id != Guid.Empty ? id : throw new MissingTenantContextException();

    private Guid RequireUserId() => tenantContext.Current?.UserId is { } id && id != Guid.Empty ? id : throw new MissingTenantContextException();
}
