namespace VepayDO.Application.Receipts.Ports;

/// <summary>
/// Turns the bytes of an upload into the bytes that are stored: it detects the
/// real content type from the magic bytes (never trusting the declared
/// <c>Content-Type</c>), re-encodes images without metadata (EXIF, including
/// GPS), and hashes the stored bytes (M5.4).
/// </summary>
public interface IReceiptImageProcessor
{
    Task<ProcessedReceiptImage> ProcessAsync(
        ReadOnlyMemory<byte> content,
        string? declaredContentType,
        CancellationToken cancellationToken = default);
}

/// <summary>The outcome of processing an upload.</summary>
public sealed record ProcessedReceiptImage(
    byte[] Content,
    string ContentType,
    int? Width,
    int? Height,
    byte[] Sha256);
