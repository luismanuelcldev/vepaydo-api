using VepayDO.Application.Storage;
using VepayDO.Domain.Receipts;

namespace VepayDO.Infrastructure.Storage;

public sealed class StorageKeyGenerator : IStorageKeyGenerator
{
    public StorageKey Generate(Guid tenantId, DateOnly day, Guid objectId) =>
        StorageKey.Create(tenantId, day, objectId);
}
