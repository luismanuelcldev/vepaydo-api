using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Receipts;

/// <summary>
/// The <c>Idempotency-Key</c> of an upload: a client-generated UUID that makes
/// a retried upload resolve to the same receipt instead of creating a second
/// one (M5.4). The key is unique per tenant, never per user, so a retry from
/// another device of the same tenant still replays the original receipt.
/// </summary>
public sealed record IdempotencyKey
{
    private IdempotencyKey(Guid value)
    {
        Value = value;
    }

    /// <summary>The key value.</summary>
    public Guid Value { get; }

    /// <summary>Parses the header value; anything that is not a UUID is rejected.</summary>
    public static IdempotencyKey Parse(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new InvalidArgumentDomainException("The Idempotency-Key header is required");
        }

        if (!Guid.TryParse(trimmed, out var value) || value == Guid.Empty)
        {
            throw new InvalidArgumentDomainException("The Idempotency-Key header must be a UUID");
        }

        return new IdempotencyKey(value);
    }

    /// <summary>True when the header value is a usable key.</summary>
    public static bool TryParse(string? raw, out IdempotencyKey? key)
    {
        try
        {
            key = Parse(raw);
            return true;
        }
        catch (InvalidArgumentDomainException)
        {
            key = null;
            return false;
        }
    }

    /// <summary>
    /// Derives a stable key from this one and a salt (the hash of the file).
    /// A request may carry several files under a single header, and each file
    /// needs its own key: the same header with the same file always resolves to
    /// the same receipt, whatever its position in the request (M5.4).
    /// </summary>
    public IdempotencyKey Derive(string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(salt);

        var payload = $"{Value:D}:{salt}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload));

        var bytes = hash[..16];

        // Shape the digest as a version 5 (name based) UUID so the stored value
        // is a well-formed identifier and never a client-chosen one.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new IdempotencyKey(new Guid(bytes));
    }
}
