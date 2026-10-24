using System.Globalization;

namespace VepayDO.Application.Tenancy;

public sealed record TenantContext(Guid? TenantId, Guid? UserId, DbRole? Role)
{

    public static readonly TenantContext None = new(null, null, null);

    public static TenantContext ForUser(Guid userId) => new(null, userId, null);

    public static TenantContext ForTenant(Guid tenantId, Guid userId, DbRole role) =>
        new(tenantId, userId, role);

    public static TenantContext ForSystem(Guid tenantId) => new(tenantId, null, DbRole.System);

    public string TenantIdSetting => TenantId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;

    public string UserIdSetting => UserId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;

    public string RoleSetting => Role?.ToDatabaseValue() ?? string.Empty;
}
