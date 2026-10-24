namespace VepayDO.Application.Tenancy;

public interface ITenantContextAccessor
{
    TenantContext? Current { get; }
}
