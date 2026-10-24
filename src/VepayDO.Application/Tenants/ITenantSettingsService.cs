using VepayDO.Application.Auth;

namespace VepayDO.Application.Tenants;

public interface ITenantSettingsService
{

    Task<TenantSettingsView> GetAsync(CancellationToken cancellationToken = default);

    Task<TenantSettingsView> UpdateAsync(
        UpdateTenantSettingsInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
