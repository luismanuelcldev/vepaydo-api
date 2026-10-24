using System.Globalization;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Receipts;

/// <summary>
/// Storage key of a receipt object: <c>{tenantId}/{yyyy}/{mm}/{uuid}</c>.
/// The tenant id is the first segment so every object of a tenant lives under
/// one prefix; a storage policy can then restrict access by prefix, and a key
/// leaked in a log never exposes another tenant's objects (AGENTS.md section 7).
/// </summary>
public sealed record StorageKey
{
    private StorageKey(Guid tenantId, DateOnly date, Guid objectId, string value)
    {
        TenantId = tenantId;
        Date = date;
        ObjectId = objectId;
        Value = value;
    }

    /// <summary>The tenant that owns the object.</summary>
    public Guid TenantId { get; }

    /// <summary>The day the object was created (tenant time zone).</summary>
    public DateOnly Date { get; }

    /// <summary>The identifier of the object (the receipt id for receipt images).</summary>
    public Guid ObjectId { get; }

    /// <summary>The full key as the storage family expects it.</summary>
    public string Value { get; }

    /// <summary>Builds the canonical key of an object.</summary>
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
