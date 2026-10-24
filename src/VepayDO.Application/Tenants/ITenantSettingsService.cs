using VepayDO.Application.Auth;

namespace VepayDO.Application.Tenants;

/// <summary>
/// Tenant settings use cases (UC-SET-04). Reads and writes run inside the
/// authenticated request transaction; the service only touches the caller's own
/// tenant.
/// </summary>
public interface ITenantSettingsService
{
    /// <summary>Returns the current tenant's settings (any active member).</summary>
    Task<TenantSettingsView> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a partial update (OWNER only, enforced by the route policy) and
    /// writes one audit event in the same transaction.
    /// </summary>
    Task<TenantSettingsView> UpdateAsync(
        UpdateTenantSettingsInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}