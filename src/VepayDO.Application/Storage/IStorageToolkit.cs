using VepayDO.Domain.Receipts;

namespace VepayDO.Application.Storage;

public interface IObjectStorage
{

    Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class StoredObject : IDisposable
{
    private readonly Stream _content;

    public StoredObject(string key, string contentType, long length, Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Key = key;
        ContentType = contentType;
        Length = length;
        _content = content;
    }

    public string Key { get; }

    public string ContentType { get; }

    public long Length { get; }

    public Stream Content => _content;

    public void Dispose() => _content.Dispose();
}

public interface ISignedUrlService
{
    Task<string> CreateSignedUrlAsync(
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    bool IsSignatureValid(
        string key,
        DateTimeOffset expiresAt,
        string? signature,
        DateTimeOffset now);
}

public interface IStorageKeyGenerator
{
    StorageKey Generate(Guid tenantId, DateOnly day, Guid objectId);
}

public interface IStorageToolkit
{
    IObjectStorage Objects { get; }

    ISignedUrlService SignedUrls { get; }

    IStorageKeyGenerator Keys { get; }
}

public interface IStorageToolkitFactory
{
    IStorageToolkit Create();
}
