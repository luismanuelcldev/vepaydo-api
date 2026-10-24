using System.Globalization;
using System.Text;

namespace VepayDO.Application.Receipts;

public sealed record ReceiptCursor(DateTimeOffset CreatedAt, Guid Id)
{
    public string Encode()
    {
        var raw = string.Create(CultureInfo.InvariantCulture, $"{CreatedAt.ToUnixTimeMilliseconds()}:{Id:D}");
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
    }

    public static ReceiptCursor Decode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new Domain.Errors.InvalidArgumentDomainException("The cursor is not valid");
        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw)); }
        catch (FormatException) { throw new Domain.Errors.InvalidArgumentDomainException("The cursor is not valid"); }

        var parts = decoded.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var millis) || !Guid.TryParse(parts[1], out var id))
            throw new Domain.Errors.InvalidArgumentDomainException("The cursor is not valid");

        return new ReceiptCursor(DateTimeOffset.FromUnixTimeMilliseconds(millis), id);
    }
}

internal static class WebEncoders
{
    public static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding is 2 or 3) normalized += new string('=', 4 - padding);
        if (padding == 1) throw new FormatException("Invalid base64url value");
        return Convert.FromBase64String(normalized);
    }
}
