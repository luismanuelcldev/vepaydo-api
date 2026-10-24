using SkiaSharp;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Receipts;

namespace VepayDO.Infrastructure.Images;

public sealed partial class SkiaReceiptImageProcessor
{
    private static (byte[] Content, int? Width, int? Height) Reencode(
        byte[] bytes,
        ReceiptContentType contentType)
    {
        using var stream = new SKMemoryStream(bytes);
        using var bitmap = SKBitmap.Decode(stream);

        if (bitmap is null) throw new UnsupportedMediaTypeException("The receipt image could not be decoded");
        if ((long)bitmap.Width * bitmap.Height > MaxPixels)
            throw new UnsupportedMediaTypeException("The receipt image is too large to process");

        var scale = Math.Min(1f, (float)MaxDimension / Math.Max(bitmap.Width, bitmap.Height));
        if (scale >= 1f) return Encode(bitmap, contentType);

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

        if (data is null) throw new UnsupportedMediaTypeException("The receipt image could not be encoded");

        return (data.ToArray(), bitmap.Width, bitmap.Height);
    }
}
