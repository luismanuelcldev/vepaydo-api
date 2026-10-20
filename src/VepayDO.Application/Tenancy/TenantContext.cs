using System.Globalization;

namespace VepayDO.Application.Tenancy;

/// <summary>
/// Identity propagated to the database through the transaction-local settings
/// <c>app.tenant_id</c>, <c>app.user_id</c>, and <c>app.role</c> (AGENTS.md section 7).
/// A <c>null</c> component is written as an empty string so the <c>app.current_*</c>
/// helpers return NULL and RLS policies fail closed.
/// </summary>
public sealed record TenantContext(Guid? TenantId, Guid? UserId, DbRole? Role)
{
    /// <summary>No identity at all; every tenant-scoped query returns zero rows.</summary>
    public static readonly TenantContext None = new(null, null, null);

    /// <summary>Pre-authentication flow: only the user id is known (login, tenant selection).</summary>
    public static TenantContext ForUser(Guid userId) => new(null, userId, null);

    /// <summary>Authenticated request/job scoped to a tenant.</summary>
    public static TenantContext ForTenant(Guid tenantId, Guid userId, DbRole role) =>
        new(tenantId, userId, role);

    /// <summary>Background work scoped to a tenant, acting as the <c>system</c> role.</summary>
    public static TenantContext ForSystem(Guid tenantId) => new(tenantId, null, DbRole.System);

    public string TenantIdSetting => TenantId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;

    public string UserIdSetting => UserId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;

    public string RoleSetting => Role?.ToDatabaseValue() ?? string.Empty;
}
