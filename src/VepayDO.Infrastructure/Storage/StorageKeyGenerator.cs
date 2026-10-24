using VepayDO.Application.Storage;
using VepayDO.Domain.Receipts;

namespace VepayDO.Infrastructure.Storage;

/// <summary>
/// The canonical key layout of every storage family: <c>{tenantId}/{yyyy}/{mm}/{uuid}</c>.
/// The tenant id is the first segment, so a bucket policy can restrict access
/// to a prefix and a leaked key never points at another tenant (M5.4).
/// </summary>
public sealed class StorageKeyGenerator : IStorageKeyGenerator
{
    public StorageKey Generate(Guid tenantId, DateOnly day, Guid objectId) =>
        StorageKey.Create(tenantId, day, objectId);
}
