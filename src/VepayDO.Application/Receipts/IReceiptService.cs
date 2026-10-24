using VepayDO.Application.Auth;
using VepayDO.Domain.Receipts;

namespace VepayDO.Application.Receipts;

/// <summary>
/// Receipt use cases (UC-RCP-01 to UC-RCP-03, M5.4): upload, list, detail and
/// a signed URL for the image. Uploads are idempotent per
/// <c>Idempotency-Key</c>, the file is described by its own bytes, and every
/// mutation and every image access is audited (AGENTS.md sections 9.8, 13).
/// </summary>
public interface IReceiptService
{
    Task<ReceiptView> UploadAsync(
        ReceiptUpload upload,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<ReceiptPage> ListAsync(
        ReceiptQuery query,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<ReceiptView> GetAsync(Guid receiptId, CancellationToken cancellationToken = default);

    Task<ReceiptImageUrl> GetImageUrlAsync(
        Guid receiptId,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}

/// <summary>Limits applied to an upload and to a signed URL.</summary>
public sealed record ReceiptOptions(int MaxUploadBytes, int SignedUrlTtlSeconds)
{
    public const int DefaultMaxUploadBytes = 10_000_000;
    public const int DefaultSignedUrlTtlSeconds = 300;

    public static ReceiptOptions Default { get; } =
        new(DefaultMaxUploadBytes, DefaultSignedUrlTtlSeconds);
}
