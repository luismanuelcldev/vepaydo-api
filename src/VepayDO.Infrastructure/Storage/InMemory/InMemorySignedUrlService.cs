using VepayDO.Application.Storage;
using VepayDO.Domain.Time;

namespace VepayDO.Infrastructure.Storage.InMemory;

/// <summary>
/// Issues the signed URLs of the in-memory family: a URL of the API itself that
/// carries the expiry and an HMAC signature, so the bytes are never served
/// without a server-issued, time-limited URL (M5.4).
/// </summary>
public sealed class InMemorySignedUrlService(
    StorageUrlSigner signer,
    string publicBaseUrl,
    IClock clock) : ISignedUrlService
{
    public Task<string> CreateSignedUrlAsync(
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var expiresAt = clock.UtcNow.Add(lifetime);
        var signature = signer.Sign(key, expiresAt);
        var path = Uri.EscapeDataString(key).Replace("%2F", "/", StringComparison.Ordinal);

        var url = $"{publicBaseUrl.TrimEnd('/')}/api/v1/storage/{path}" +
                  $"?expires={expiresAt.ToUnixTimeSeconds()}&signature={signature}";

        return Task.FromResult(url);
    }

    public bool IsSignatureValid(
        string key,
        DateTimeOffset expiresAt,
        string? signature,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return signer.Verify(key, expiresAt, signature, now);
    }
}
