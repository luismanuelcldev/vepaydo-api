using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tenants.Ports;

public interface ITenantSettingsRepository
{

    Task<TenantSettingsValues?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid tenantId, TenantSettingsValues values, CancellationToken cancellationToken = default);
}
