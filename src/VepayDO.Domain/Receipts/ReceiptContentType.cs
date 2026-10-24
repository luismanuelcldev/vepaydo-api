using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Receipts;

/// <summary>
/// The content types a receipt may have, decided by the bytes of the file and
/// never by the <c>Content-Type</c> the client declared (M5.4). A declared type
/// that contradicts the sniffed type is rejected; a generic declaration
/// (<c>application/octet-stream</c>) is simply ignored.
/// </summary>
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

    /// <summary>The canonical MIME type stored with the receipt.</summary>
    public string Value { get; }

    /// <summary>Whether the content is a raster image (re-encoded) or a document (stored as is).</summary>
    public ReceiptContentKind Kind { get; }

    public static ReceiptContentType Jpeg { get; } = new(JpegMime, ReceiptContentKind.Image);

    public static ReceiptContentType Png { get; } = new(PngMime, ReceiptContentKind.Image);

    public static ReceiptContentType Pdf { get; } = new(PdfMime, ReceiptContentKind.Document);

    /// <summary>Every supported content type.</summary>
    public static IReadOnlyList<ReceiptContentType> Supported { get; } = [Jpeg, Png, Pdf];

    /// <summary>True when the type is one the server can store.</summary>
    public static bool IsSupported(string? mime) =>
        mime is not null && Supported.Any(t => string.Equals(t.Value, mime, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves the effective content type: the sniffed type wins, and a declared
    /// type that contradicts it is an error (the client sent something else than
    /// it claimed).
    /// </summary>
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

/// <summary>Whether a receipt file is re-encoded (image) or stored untouched (document).</summary>
public enum ReceiptContentKind
{
    Image,
    Document,
}

/// <summary>The uploaded bytes are not a supported receipt file (AGENTS.md section 3.2).</summary>
public sealed class UnsupportedMediaTypeException : DomainException
{
    public UnsupportedMediaTypeException(string message = "Unsupported media type")
        : base(message, httpStatus: 415)
    {
    }

    public override string Code => "RECEIPT.UNSUPPORTED_MEDIA_TYPE";

    public override string I18nKey => "receipt.unsupported_media_type";
}
