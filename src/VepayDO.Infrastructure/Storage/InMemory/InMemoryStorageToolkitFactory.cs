using VepayDO.Application.Storage;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Storage;

namespace VepayDO.Infrastructure.Storage.InMemory;

public sealed class InMemoryStorageToolkitFactory : IStorageToolkitFactory
{
    private readonly IStorageToolkit _toolkit;

    public InMemoryStorageToolkitFactory(StorageUrlSigner signer, string publicBaseUrl, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicBaseUrl);
        ArgumentNullException.ThrowIfNull(clock);

        _toolkit = new Toolkit(
            new InMemoryObjectStorage(),
            new InMemorySignedUrlService(signer, publicBaseUrl, clock),
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
