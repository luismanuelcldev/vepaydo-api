using Amazon;
using Amazon.S3;
using VepayDO.Application.Storage;

namespace VepayDO.Infrastructure.Storage.S3;

public sealed class S3StorageToolkitFactory : IStorageToolkitFactory
{
    private readonly IStorageToolkit _toolkit;

    public S3StorageToolkitFactory(IAmazonS3 client, string bucket)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);

        _toolkit = new Toolkit(
            new S3ObjectStorage(client, bucket),
            new S3SignedUrlService(client, bucket),
            new StorageKeyGenerator());
    }

    public IStorageToolkit Create() => _toolkit;

    private sealed class Toolkit(
        IObjectStorage objects,
        ISignedUrlService signedUrls,
        IStorageKeyGenerator keys) : IStorageToolkit
    {
        public IObjectStorage Objects { get; } = objects;

        public ISignedUrlService SignedUrls { get; } = signedUrls;

        public IStorageKeyGenerator Keys { get; } = keys;
    }
}
