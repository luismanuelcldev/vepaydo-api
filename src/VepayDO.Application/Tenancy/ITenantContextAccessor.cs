namespace VepayDO.Application.Tenancy;

/// <summary>
/// Reads the tenant context installed for the current async flow. Returns
/// <c>null</c> outside a tenant transaction, so callers must decide how to fail.
/// </summary>
public interface ITenantContextAccessor
{
    TenantContext? Current { get; }
}
