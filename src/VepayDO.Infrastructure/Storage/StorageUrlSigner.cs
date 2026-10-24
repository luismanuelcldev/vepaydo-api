using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VepayDO.Infrastructure.Storage;

public sealed class StorageUrlSigner
{
    private readonly byte[] _key;

    public StorageUrlSigner(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("A signing secret is required.", nameof(secret));
        }

        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Sign(string key, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var payload = $"{key}|{expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}";
        var signature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    public bool Verify(string key, DateTimeOffset expiresAt, string? signature, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(signature) || expiresAt <= now)
        {
            return false;
        }

        var expected = Sign(key, expiresAt);
        var actual = signature.Trim().ToLowerInvariant();

        if (expected.Length != actual.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }
}
