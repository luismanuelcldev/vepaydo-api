using System.Collections.Concurrent;
using VepayDO.Application.Storage;

namespace VepayDO.Infrastructure.Storage.InMemory;

/// <summary>
/// In-memory storage family: the bytes live in the process. It is the family
/// used by the tests and by a local run without an object store; it is not
/// durable, and the API says so when it is the configured one (M5.4).
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (string ContentType, byte[] Content)> _objects = new(StringComparer.Ordinal);

    public Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(contentType);

        _objects[key] = (contentType, content.ToArray());
        return Task.CompletedTask;
    }

    public Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_objects.TryGetValue(key, out var entry))
        {
            return Task.FromResult<StoredObject?>(null);
        }

        var stored = new StoredObject(
            key,
            entry.ContentType,
            entry.Content.Length,
            new MemoryStream(entry.Content, writable: false));

        return Task.FromResult<StoredObject?>(stored);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Task.FromResult(_objects.ContainsKey(key));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
