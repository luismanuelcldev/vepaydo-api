using System.Security.Cryptography;
using SkiaSharp;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Receipts;

namespace VepayDO.Infrastructure.Images;

/// <summary>
/// Processes an uploaded receipt with SkiaSharp (M5.4):
///
/// <list type="bullet">
///   <item>the content type comes from the magic bytes, never from the declared
///   <c>Content-Type</c>; a declaration that contradicts the bytes is rejected;</item>
///   <item>images are re-encoded, which drops every metadata segment (EXIF, and
///   with it the GPS coordinates of the phone that took the picture);</item>
///   <item>the SHA-256 is computed over the bytes that are actually stored.</item>
/// </list>
///
/// </summary>
public sealed class SkiaReceiptImageProcessor : IReceiptImageProcessor
{
    /// <summary>Quality of the re-encoded JPEG.</summary>
    public const int JpegQuality = 85;

    /// <summary>Longest edge kept, so a huge photo cannot blow up the storage.</summary>
    public const int MaxDimension = 4096;

    /// <summary>Refused above this many pixels (decompression-bomb guard).</summary>
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

    /// <summary>Detects the real content type from the first bytes of the file.</summary>
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

        throw new UnsupportedMediaTypeException(
            "The file is not a supported receipt image or document");
    }

    private static (byte[] Content, int? Width, int? Height) Reencode(
        byte[] bytes,
        ReceiptContentType contentType)
    {
        using var stream = new SKMemoryStream(bytes);
        using var bitmap = SKBitmap.Decode(stream);

        if (bitmap is null)
        {
            throw new UnsupportedMediaTypeException("The receipt image could not be decoded");
        }

        if ((long)bitmap.Width * bitmap.Height > MaxPixels)
        {
            throw new UnsupportedMediaTypeException("The receipt image is too large to process");
        }

        var scale = Math.Min(
            1f,
            (float)MaxDimension / Math.Max(bitmap.Width, bitmap.Height));

        if (scale >= 1f)
        {
            return Encode(bitmap, contentType);
        }

        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        using var scaled = new SKBitmap(width, height, bitmap.ColorType, bitmap.AlphaType);
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, width, height));
            canvas.Flush();
        }

        return Encode(scaled, contentType);
    }

    private static (byte[] Content, int? Width, int? Height) Encode(
        SKBitmap bitmap,
        ReceiptContentType contentType)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = contentType == ReceiptContentType.Png
            ? image.Encode(SKEncodedImageFormat.Png, 100)
            : image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        if (data is null)
        {
            throw new UnsupportedMediaTypeException("The receipt image could not be encoded");
        }

        return (data.ToArray(), bitmap.Width, bitmap.Height);
    }
}
