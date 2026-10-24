using VepayDO.Domain.Receipts;

namespace VepayDO.Application.Storage;

/// <summary>
/// Object storage port: the only member of the storage family that touches bytes.
/// Implementations live in Infrastructure (in-memory and S3-compatible); the
/// application layer never learns which family is configured (M5.4).
/// </summary>
public interface IObjectStorage
{
    /// <summary>Stores an object, replacing any object with the same key.</summary>
    Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Reads an object, or <c>null</c> when the key does not exist.</summary>
    Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>True when the key exists.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes an object; a missing key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>An object read from the storage family.</summary>
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

    /// <summary>The object bytes.</summary>
    public Stream Content => _content;

    public void Dispose() => _content.Dispose();
}

/// <summary>
/// Issues a time-limited URL that lets the client read an object without
/// credentials. S3 answers with a presigned GET; the in-memory family signs a
/// URL of the API itself. The client always asks the API for the URL, so
/// swapping the family changes nothing for the client (M5.4).
/// </summary>
public interface ISignedUrlService
{
    Task<string> CreateSignedUrlAsync(
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the signature and the expiry of a URL of this family are valid.
    /// The S3 family answers <c>false</c>: its URLs are validated by the object
    /// store itself and never reach the API (M5.4).
    /// </summary>
    bool IsSignatureValid(
        string key,
        DateTimeOffset expiresAt,
        string? signature,
        DateTimeOffset now);
}

/// <summary>Builds the storage key of an object.</summary>
public interface IStorageKeyGenerator
{
    StorageKey Generate(Guid tenantId, DateOnly day, Guid objectId);
}

/// <summary>
/// The complete storage toolkit of one family: object storage, signed URLs and
/// key generation. The application layer only ever sees this interface.
/// </summary>
public interface IStorageToolkit
{
    IObjectStorage Objects { get; }

    ISignedUrlService SignedUrls { get; }

    IStorageKeyGenerator Keys { get; }
}

/// <summary>
/// Abstract Factory of the storage toolkit (M5.4). Each family ships its own
/// implementation; the composition root picks one from the configuration.
/// </summary>
public interface IStorageToolkitFactory
{
    IStorageToolkit Create();
}
