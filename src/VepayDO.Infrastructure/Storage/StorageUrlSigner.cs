using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VepayDO.Infrastructure.Storage;

/// <summary>
/// Signs the URLs of the in-memory storage family: an HMAC-SHA256 over the key
/// and the expiry, so a URL can only be produced by the server and only until
/// it expires (M5.4). The S3 family answers with a presigned URL instead and
/// does not use this signer.
/// </summary>
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

    /// <summary>Builds the signature of a key at a given moment.</summary>
    public string Sign(string key, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var payload = $"{key}|{expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}";
        var signature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    /// <summary>True when the signature matches and the URL has not expired.</summary>
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
