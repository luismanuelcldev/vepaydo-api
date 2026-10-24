using System.Security.Cryptography;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Receipts;

namespace VepayDO.Infrastructure.Images;

public sealed partial class SkiaReceiptImageProcessor : IReceiptImageProcessor
{
    public const int JpegQuality = 85;
    public const int MaxDimension = 4096;
    public const int MaxPixels = 40_000_000;

    public Task<ProcessedReceiptImage> ProcessAsync(
        ReadOnlyMemory<byte> content,
        string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        var bytes = content.ToArray();
        if (bytes.Length == 0)
        {
            throw new InvalidArgumentDomainException("The receipt file is empty");
        }

        var sniffed = Sniff(bytes);
        var effective = ReceiptContentType.Resolve(declaredContentType, sniffed);

        var (stored, width, height) = effective.Kind == ReceiptContentKind.Document
            ? (bytes, (int?)null, (int?)null)
            : Reencode(bytes, effective);

        var sha256 = SHA256.HashData(stored);

        return Task.FromResult(new ProcessedReceiptImage(
            stored,
            effective.Value,
            width,
            height,
            sha256));
    }

    public static ReceiptContentType Sniff(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ReceiptContentType.Jpeg;
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ReceiptContentType.Png;
        }

        if (bytes.Length >= 4
            && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
        {
            return ReceiptContentType.Pdf;
        }

        throw new UnsupportedMediaTypeException("The file is not a supported receipt image or document");
    }
}
