namespace VepayDO.Application.Receipts.Ports;

public interface IReceiptImageProcessor
{
    Task<ProcessedReceiptImage> ProcessAsync(
        ReadOnlyMemory<byte> content,
        string? declaredContentType,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessedReceiptImage(
    byte[] Content,
    string ContentType,
    int? Width,
    int? Height,
    byte[] Sha256);
