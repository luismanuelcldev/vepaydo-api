using VepayDO.Application.Auth;
using VepayDO.Application.Receipts;

namespace VepayDO.Api.Tests.Fixtures;

public sealed class FakeReceiptService : IReceiptService
{
    public List<ReceiptView> Items { get; } = [];

    public ReceiptImageUrl LastImageUrl { get; set; } =
        new("https://example.com/receipts/test.jpg?signature=abc", DateTimeOffset.UtcNow.AddMinutes(5));

    public Task<ReceiptView> UploadAsync(
        ReceiptUpload upload,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var view = new ReceiptView(
            id,
            upload.Source.ToString(),
            "RECEIVED",
            "PENDING_REVIEW",
            null,
            "image/jpeg",
            upload.Content.Length,
            800,
            600,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Items.Insert(0, view);
        return Task.FromResult(view);
    }

    public Task<ReceiptPage> ListAsync(
        ReceiptQuery query,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var items = Items.ToList();
        return Task.FromResult(new ReceiptPage(items, null));
    }

    public Task<ReceiptView> GetAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var item = Items.FirstOrDefault(r => r.Id == receiptId)
                   ?? throw new VepayDO.Domain.Errors.NotFoundException("Receipt not found");
        return Task.FromResult(item);
    }

    public Task<ReceiptImageUrl> GetImageUrlAsync(
        Guid receiptId,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        if (!Items.Any(r => r.Id == receiptId))
        {
            throw new VepayDO.Domain.Errors.NotFoundException("Receipt not found");
        }

        return Task.FromResult(LastImageUrl);
    }
}
