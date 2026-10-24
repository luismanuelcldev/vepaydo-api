using VepayDO.Application.Auth;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Receipts;

public sealed partial class ReceiptService
{
    public async Task<ReceiptView> UploadAsync(ReceiptUpload upload, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        if (upload.Content.Length == 0) throw new InvalidArgumentDomainException("The receipt file is empty");
        if (upload.Content.Length > options.MaxUploadBytes) throw new PayloadTooLargeException($"The receipt file must be at most {options.MaxUploadBytes} bytes");

        var fileKey = upload.IdempotencyKey.Derive(HashOf(upload.Content));
        var existing = await receipts.FindByIdempotencyKeyAsync(tenantId, fileKey.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return ToView(existing);

        var processed = await images.ProcessAsync(upload.Content, upload.DeclaredContentType, cancellationToken).ConfigureAwait(false);
        var receiptId = Guid.NewGuid();
        var today = clock.Today(TenantTimeZone.Get(TenantTimeZone.Default));
        var key = storage.Keys.Generate(tenantId, today, receiptId);

        await storage.Objects.PutAsync(key.Value, processed.Content, processed.ContentType, cancellationToken).ConfigureAwait(false);

        bool inserted;
        try
        {
            inserted = await receipts.AddAsync(
                tenantId,
                new NewReceipt(receiptId, fileKey.Value, RequireUserId(), upload.Source.ToString(), key.Value, processed.ContentType, processed.Content.Length, processed.Width, processed.Height, processed.Sha256),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await TryDeleteAsync(key.Value, cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (!inserted)
        {
            await TryDeleteAsync(key.Value, cancellationToken).ConfigureAwait(false);
            var winner = await receipts.FindByIdempotencyKeyAsync(tenantId, fileKey.Value, cancellationToken).ConfigureAwait(false)
                ?? throw new ConflictException("A receipt with this idempotency key already exists");
            return ToView(winner);
        }

        try
        {
            await queue.EnqueueAsync(receiptId, tenantId, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(tenantId, receiptId, "upload", before: null, after: new
            {
                source = upload.Source.ToString(),
                mime = processed.ContentType,
                bytes = processed.Content.Length,
                width = processed.Width,
                height = processed.Height,
                sha256 = Convert.ToHexString(processed.Sha256).ToLowerInvariant(),
            }, meta, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await TryDeleteAsync(key.Value, cancellationToken).ConfigureAwait(false);
            throw;
        }

        var stored = await receipts.FindAsync(tenantId, receiptId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Receipt not found");
        return ToView(stored);
    }

    private async Task TryDeleteAsync(string key, CancellationToken cancellationToken)
    {
        try { await storage.Objects.DeleteAsync(key, cancellationToken).ConfigureAwait(false); }
        catch (DomainException) { }
    }
}
