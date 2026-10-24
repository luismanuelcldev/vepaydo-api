using Amazon.S3;
using Amazon.S3.Model;
using VepayDO.Application.Storage;

namespace VepayDO.Infrastructure.Storage.S3;

public sealed class S3SignedUrlService(IAmazonS3 client, string bucket) : ISignedUrlService
{
    public Task<string> CreateSignedUrlAsync(
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);

        var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(lifetime),
            Verb = HttpVerb.GET,
        });

        return Task.FromResult(url);
    }

    public bool IsSignatureValid(
        string key,
        DateTimeOffset expiresAt,
        string? signature,
        DateTimeOffset now)
    {

        return false;
    }
}
