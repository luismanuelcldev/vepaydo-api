using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tenants.Ports;

/// <summary>
/// Reads and writes the current tenant's <c>tenant_settings</c> row. The table is
/// protected by the simple <c>tenant_isolation</c> RLS policy, so every call
/// runs inside the request transaction with <c>app.tenant_id</c> set (AGENTS.md
/// section 7).
/// </summary>
public interface ITenantSettingsRepository
{
    /// <summary>Returns the current tenant's settings, or <c>null</c> when the row is missing.</summary>
    Task<TenantSettingsValues?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Persists the given settings for the current tenant.</summary>
    Task UpdateAsync(Guid tenantId, TenantSettingsValues values, CancellationToken cancellationToken = default);
}