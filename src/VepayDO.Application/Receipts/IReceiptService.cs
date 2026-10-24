using VepayDO.Application.Auth;
using VepayDO.Domain.Receipts;

namespace VepayDO.Application.Receipts;

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

public sealed record ReceiptOptions(int MaxUploadBytes, int SignedUrlTtlSeconds)
{
    public const int DefaultMaxUploadBytes = 10_000_000;
    public const int DefaultSignedUrlTtlSeconds = 300;

    public static ReceiptOptions Default { get; } =
        new(DefaultMaxUploadBytes, DefaultSignedUrlTtlSeconds);
}
