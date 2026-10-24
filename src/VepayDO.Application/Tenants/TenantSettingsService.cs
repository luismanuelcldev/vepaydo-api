using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tenants;

public sealed class TenantSettingsService(
    ITenantSettingsRepository settings,
    IAuditWriter audit,
    ITenantContextAccessor tenantContext) : ITenantSettingsService
{
    public async Task<TenantSettingsView> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var values = await settings.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (values is null)
        {
            throw new NotFoundException("Tenant settings not found");
        }

        return TenantSettingsView.From(values);
    }

    public async Task<TenantSettingsView> UpdateAsync(
        UpdateTenantSettingsInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var current = await settings.GetAsync(tenantId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Tenant settings not found");

        var updated = input.ApplyTo(current);

        updated.Validate();

        await settings.UpdateAsync(tenantId, updated, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("tenant_settings")
                .EntityId(tenantId)
                .Action("update")
                .Before(TenantSettingsView.From(current))
                .After(TenantSettingsView.From(updated))
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        return TenantSettingsView.From(updated);
    }

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        return tenantId.Value;
    }
}
