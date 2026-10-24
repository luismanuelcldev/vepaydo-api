using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Application.Storage;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Receipts;

/// <summary>
/// Receipt use cases (UC-RCP-01 to UC-RCP-03, M5.4).
///
/// <para>
/// An upload is idempotent per <c>Idempotency-Key</c>: a replay returns the
/// receipt the first call created, without storing the bytes twice. The file is
/// described by its own bytes (magic-byte sniffing), images are re-encoded
/// without metadata, and the SHA-256 is computed over the stored bytes.
/// </para>
///
/// <para>
/// The object is written before the row, and the row (with its audit event and
/// its queued job) is written in the request's transaction. If the transaction
/// fails, the orphaned object is deleted: the storage never keeps a file that
/// no receipt points to.
/// </para>
/// </summary>
public sealed class ReceiptService(
    IReceiptRepository receipts,
    IReceiptImageProcessor images,
    IStorageToolkit storage,
    IReceiptJobStore queue,
    IAuditWriter audit,
    IClock clock,
    ReceiptOptions options,
    ITenantContextAccessor tenantContext) : IReceiptService
{
    public async Task<ReceiptView> UploadAsync(
        ReceiptUpload upload,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();

        if (upload.Content.Length == 0)
        {
            throw new InvalidArgumentDomainException("The receipt file is empty");
        }

        if (upload.Content.Length > options.MaxUploadBytes)
        {
            throw new PayloadTooLargeException(
                $"The receipt file must be at most {options.MaxUploadBytes} bytes");
        }

        // The header identifies the upload request; a request may carry several
        // files, and each one needs its own key, otherwise the batch would
        // collapse into a single receipt. The salt is the hash of the uploaded
        // bytes, so the same file under the same header always resolves to the
        // same receipt, whatever its position in the request.
        var fileKey = upload.IdempotencyKey.Derive(HashOf(upload.Content));

        // Replay: the same key always resolves to the same receipt.
        var existing = await receipts
            .FindByIdempotencyKeyAsync(tenantId, fileKey.Value, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return ToView(existing);
        }

        var processed = await images
            .ProcessAsync(upload.Content, upload.DeclaredContentType, cancellationToken)
            .ConfigureAwait(false);

        var receiptId = Guid.NewGuid();
        var today = clock.Today(TenantTimeZone.Get(TenantTimeZone.Default));
        var key = storage.Keys.Generate(tenantId, today, receiptId);

        await storage.Objects
            .PutAsync(key.Value, processed.Content, processed.ContentType, cancellationToken)
            .ConfigureAwait(false);

        bool inserted;
        try
        {
            inserted = await receipts.AddAsync(
                tenantId,
                new NewReceipt(
                    receiptId,
                    fileKey.Value,
                    RequireUserId(),
                    upload.Source.ToString(),
                    key.Value,
                    processed.ContentType,
                    processed.Content.Length,
                    processed.Width,
                    processed.Height,
                    processed.Sha256),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The row never landed: the object it would point to is an orphan.
            await TryDeleteAsync(key.Value, cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (!inserted)
        {
            // A concurrent upload with the same idempotency key won the race: the
            // stored bytes of that one are the ones the receipt points to.
            await TryDeleteAsync(key.Value, cancellationToken).ConfigureAwait(false);

            var winner = await receipts
                .FindByIdempotencyKeyAsync(tenantId, fileKey.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new ConflictException("A receipt with this idempotency key already exists");

            return ToView(winner);
        }

        try
        {
            await queue.EnqueueAsync(receiptId, tenantId, cancellationToken).ConfigureAwait(false);

            await WriteAuditAsync(
                tenantId,
                receiptId,
                "upload",
                before: null,
                after: new
                {
                    source = upload.Source.ToString(),
                    mime = processed.ContentType,
                    bytes = processed.Content.Length,
                    width = processed.Width,
                    height = processed.Height,
                    sha256 = Convert.ToHexString(processed.Sha256).ToLowerInvariant(),
                },
                meta,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The row never landed: the object it would point to is an orphan.
            await TryDeleteAsync(key.Value, cancellationToken).ConfigureAwait(false);
            throw;
        }

        var stored = await receipts.FindAsync(tenantId, receiptId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Receipt not found");

        return ToView(stored);
    }

    public async Task<ReceiptPage> ListAsync(
        ReceiptQuery query,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = RequireTenantId();

        // The filters are compared against the enum labels of PostgreSQL, so
        // they are normalized to the canonical name of the enum: "extracted"
        // and "EXTRACTED" are the same filter, and a value that is not a
        // member of the enum is rejected instead of silently matching nothing.
        var normalized = query with
        {
            ProcessingStatus = Normalize<ProcessingStatus>(query.ProcessingStatus, "Processing status"),
            ReconciliationStatus = Normalize<ReconciliationStatus>(query.ReconciliationStatus, "Reconciliation status"),
            Risk = Normalize<AlertSeverity>(query.Risk, "Risk"),
        };

        var limit = Math.Clamp(query.Limit, 1, 100);
        var position = string.IsNullOrWhiteSpace(cursor) ? null : ReceiptCursor.Decode(cursor);

        // One extra row tells the client whether there is another page.
        var rows = await receipts
            .ListAsync(tenantId, normalized with { Limit = limit + 1 }, position, cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToList();

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            nextCursor = new ReceiptCursor(last.CreatedAt, last.Id).Encode();
        }

        return new ReceiptPage(page.Select(ToView).ToList(), nextCursor);
    }

    public async Task<ReceiptView> GetAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var record = await receipts.FindAsync(tenantId, receiptId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Receipt not found");

        return ToView(record);
    }

    public async Task<ReceiptImageUrl> GetImageUrlAsync(
        Guid receiptId,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var record = await receipts.FindAsync(tenantId, receiptId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Receipt not found");

        if (record.ImageDeletedAt is not null)
        {
            throw new NotFoundException("The receipt image is no longer available");
        }

        var lifetime = TimeSpan.FromSeconds(Math.Max(30, options.SignedUrlTtlSeconds));
        var url = await storage.SignedUrls
            .CreateSignedUrlAsync(record.StorageKey, lifetime, cancellationToken)
            .ConfigureAwait(false);

        await WriteAuditAsync(
            tenantId,
            receiptId,
            "image.access",
            before: null,
            after: new { expiresInSeconds = (int)lifetime.TotalSeconds },
            meta,
            cancellationToken).ConfigureAwait(false);

        return new ReceiptImageUrl(url, clock.UtcNow.Add(lifetime));
    }

    private async Task TryDeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await storage.Objects.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (DomainException)
        {
            // Never mask the original failure with a cleanup failure.
        }
    }

    private Task WriteAuditAsync(
        Guid tenantId,
        Guid entityId,
        string action,
        object? before,
        object? after,
        RequestMeta meta,
        CancellationToken cancellationToken)
    {
        var draft = new AuditEventBuilder()
            .TenantId(tenantId)
            .UserId(tenantContext.Current?.UserId)
            .Entity("receipt")
            .EntityId(entityId)
            .Action(action)
            .Before(before)
            .After(after)
            .Ip(meta.Ip)
            .Build();

        return audit.WriteAsync(draft, cancellationToken);
    }

    private static string? Normalize<TEnum>(string? value, string label)
        where TEnum : struct, System.Enum
    {
        if (value is null)
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new InvalidArgumentDomainException(
                $"{label} must be one of {string.Join(", ", Enum.GetNames<TEnum>())}");
        }

        return parsed.ToString();
    }

    private static string HashOf(byte[] content) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

    private static ReceiptView ToView(ReceiptRecord record) =>
        new(
            record.Id,
            record.Source,
            record.ProcessingStatus,
            record.ReconciliationStatus,
            record.Risk,
            record.Mime,
            record.Bytes,
            record.Width,
            record.Height,
            record.Amount,
            record.Currency,
            record.PaymentDate,
            record.Reference,
            record.SenderName,
            record.DestinationLast4,
            record.CustomerId,
            record.SuggestedChargeId,
            record.MatchScore,
            record.FailureReason,
            record.CreatedAt,
            record.UpdatedAt);

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        return tenantId.Value;
    }

    private Guid RequireUserId()
    {
        var userId = tenantContext.Current?.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        return userId.Value;
    }
}
