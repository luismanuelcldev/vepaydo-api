using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using VepayDO.Application.Storage;

namespace VepayDO.Infrastructure.Storage.S3;

public sealed class S3ObjectStorage(IAmazonS3 client, string bucket) : IObjectStorage
{
    public async Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentNullException.ThrowIfNull(contentType);

        using var buffer = new MemoryStream(content.ToArray(), writable: false);

        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = buffer,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var response = await client.GetObjectAsync(
                new GetObjectRequest { BucketName = bucket, Key = key },
                cancellationToken).ConfigureAwait(false);

            return new StoredObject(
                key,
                response.Headers.ContentType ?? "application/octet-stream",
                response.Headers.ContentLength,
                response.ResponseStream);
        }
        catch (AmazonServiceException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = bucket, Key = key },
                cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (AmazonServiceException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = bucket, Key = key },
            cancellationToken).ConfigureAwait(false);
    }
}
