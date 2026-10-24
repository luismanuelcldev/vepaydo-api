using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Receipts;

public sealed record IdempotencyKey
{
    private IdempotencyKey(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

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

    public IdempotencyKey Derive(string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(salt);

        var payload = $"{Value:D}:{salt}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload));

        var bytes = hash[..16];

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new IdempotencyKey(new Guid(bytes));
    }
}
