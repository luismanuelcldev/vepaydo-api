using System.Globalization;
using System.Text;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Receipts;

namespace VepayDO.Application.Receipts;

/// <summary>
/// A receipt as returned by the API (UC-RCP-01 to UC-RCP-03). The image itself
/// is never inlined: the client asks for a signed URL (UC-RCP-03).
/// </summary>
public sealed record ReceiptView(
    Guid Id,
    string Source,
    string ProcessingStatus,
    string ReconciliationStatus,
    string? Risk,
    string Mime,
    int Bytes,
    int? Width,
    int? Height,
    decimal? Amount,
    string? Currency,
    DateOnly? PaymentDate,
    string? Reference,
    string? SenderName,
    string? DestinationLast4,
    Guid? CustomerId,
    Guid? SuggestedChargeId,
    decimal? MatchScore,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A receipt row as stored, including the fields the API does not expose.</summary>
public sealed record ReceiptRecord(
    Guid Id,
    Guid IdempotencyKey,
    Guid UploadedBy,
    string Source,
    string StorageKey,
    string Mime,
    int Bytes,
    int? Width,
    int? Height,
    byte[] Sha256,
    string ProcessingStatus,
    string ReconciliationStatus,
    string? Risk,
    decimal? Amount,
    string? Currency,
    DateOnly? PaymentDate,
    string? Reference,
    string? SenderName,
    string? DestinationLast4,
    Guid? CustomerId,
    Guid? SuggestedChargeId,
    decimal? MatchScore,
    string? FailureReason,
    DateTimeOffset? ImageDeletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Filters of the receipt list (M5.4).</summary>
public sealed record ReceiptQuery(
    string? ProcessingStatus,
    string? ReconciliationStatus,
    string? Risk,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    int Limit);

/// <summary>The keyset position of the last item of a page.</summary>
public sealed record ReceiptCursor(DateTimeOffset CreatedAt, Guid Id)
{
    /// <summary>Encodes the cursor as an opaque token for the client.</summary>
    public string Encode()
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{CreatedAt.ToUnixTimeMilliseconds()}:{Id:D}");

        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>Decodes a cursor token; an invalid token is an error, never a silent reset.</summary>
    public static ReceiptCursor Decode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new Domain.Errors.InvalidArgumentDomainException("The cursor is not valid");
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw));
        }
        catch (FormatException)
        {
            throw new Domain.Errors.InvalidArgumentDomainException("The cursor is not valid");
        }

        var parts = decoded.Split(':');
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var millis)
            || !Guid.TryParse(parts[1], out var id))
        {
            throw new Domain.Errors.InvalidArgumentDomainException("The cursor is not valid");
        }

        return new ReceiptCursor(DateTimeOffset.FromUnixTimeMilliseconds(millis), id);
    }
}

/// <summary>One page of receipts plus the token of the next page.</summary>
public sealed record ReceiptPage(IReadOnlyList<ReceiptView> Items, string? NextCursor);

/// <summary>A receipt waiting to be processed by the worker.</summary>
public sealed record ReceiptJob(Guid Id, Guid TenantId, string Kind, int Attempts, DateTimeOffset AvailableAt);

/// <summary>Everything the service needs to store an uploaded receipt.</summary>
public sealed record ReceiptUpload(
    IdempotencyKey IdempotencyKey,
    ReceiptSource Source,
    string? DeclaredContentType,
    byte[] Content);

/// <summary>A time-limited URL that lets the client read the receipt image.</summary>
public sealed record ReceiptImageUrl(string Url, DateTimeOffset ExpiresAt);

/// <summary>Base64url helpers (RFC 4648 section 5) without padding.</summary>
internal static class WebEncoders
{
    public static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding is 2 or 3)
        {
            normalized += new string('=', 4 - padding);
        }

        if (padding == 1)
        {
            throw new FormatException("Invalid base64url value");
        }

        return Convert.FromBase64String(normalized);
    }
}
