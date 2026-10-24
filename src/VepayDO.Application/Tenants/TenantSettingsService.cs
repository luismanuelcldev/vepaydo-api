using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tenants;

/// <summary>
/// Tenant settings use cases (UC-SET-04). Settings are always read and written
/// inside the authenticated request transaction, so the tenant comes from the
/// ambient context rather than from the payload — a caller can never address
/// another tenant's settings.
/// </summary>
public sealed class TenantSettingsService(
    ITenantSettingsRepository settings,
    IAuditWriter audit,
    ITenantContextAccessor tenantContext) : ITenantSettingsService
{
    public async Task<TenantSettingsView> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var values = await settings.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);

        // A tenant is always created with a settings row (app.create_tenant); a
        // missing row means the request is not scoped to a real tenant.
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

        // Domain invariants are re-checked here (not only in FluentValidation) so
        // the rule lives in exactly one place and any future caller obeys it.
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