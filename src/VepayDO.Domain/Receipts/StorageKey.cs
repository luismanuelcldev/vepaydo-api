using System.Globalization;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Receipts;

public sealed record StorageKey
{
    private StorageKey(Guid tenantId, DateOnly date, Guid objectId, string value)
    {
        TenantId = tenantId;
        Date = date;
        ObjectId = objectId;
        Value = value;
    }

    public Guid TenantId { get; }

    public DateOnly Date { get; }

    public Guid ObjectId { get; }

    public string Value { get; }

    public static StorageKey Create(Guid tenantId, DateOnly date, Guid objectId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidArgumentDomainException("A storage key requires a tenant id");
        }

        if (objectId == Guid.Empty)
        {
            throw new InvalidArgumentDomainException("A storage key requires an object id");
        }

        var year = date.ToString("yyyy", CultureInfo.InvariantCulture);
        var month = date.ToString("MM", CultureInfo.InvariantCulture);
        var value = $"{tenantId:D}/{year}/{month}/{objectId:D}";

        return new StorageKey(tenantId, date, objectId, value);
    }

    public override string ToString() => Value;
}
