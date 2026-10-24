using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Receipts;

public sealed record ReceiptContentType
{
    public const string JpegMime = "image/jpeg";
    public const string PngMime = "image/png";
    public const string PdfMime = "application/pdf";
    public const string OctetStreamMime = "application/octet-stream";

    private ReceiptContentType(string value, ReceiptContentKind kind)
    {
        Value = value;
        Kind = kind;
    }

    public string Value { get; }

    public ReceiptContentKind Kind { get; }

    public static ReceiptContentType Jpeg { get; } = new(JpegMime, ReceiptContentKind.Image);

    public static ReceiptContentType Png { get; } = new(PngMime, ReceiptContentKind.Image);

    public static ReceiptContentType Pdf { get; } = new(PdfMime, ReceiptContentKind.Document);

    public static IReadOnlyList<ReceiptContentType> Supported { get; } = [Jpeg, Png, Pdf];

    public static bool IsSupported(string? mime) =>
        mime is not null && Supported.Any(t => string.Equals(t.Value, mime, StringComparison.OrdinalIgnoreCase));

    public static ReceiptContentType Resolve(string? declaredMime, ReceiptContentType sniffed)
    {
        ArgumentNullException.ThrowIfNull(sniffed);

        var declared = declaredMime?.Trim().ToLowerInvariant() ?? string.Empty;

        if (declared.Length == 0
            || string.Equals(declared, OctetStreamMime, StringComparison.Ordinal)
            || string.Equals(declared, sniffed.Value, StringComparison.Ordinal))
        {
            return sniffed;
        }

        throw new UnsupportedMediaTypeException(
            $"The declared content type '{declared}' does not match the detected type '{sniffed.Value}'");
    }
}

public enum ReceiptContentKind
{
    Image,
    Document,
}

public sealed class UnsupportedMediaTypeException : DomainException
{
    public UnsupportedMediaTypeException(string message = "Unsupported media type")
        : base(message, httpStatus: 415)
    {
    }

    public override string Code => "RECEIPT.UNSUPPORTED_MEDIA_TYPE";

    public override string I18nKey => "receipt.unsupported_media_type";
}
