using VepayDO.Application.Tenancy;

namespace VepayDO.Infrastructure.Tenancy;

public sealed class TenantContextAccessor(TenantContextStore store) : ITenantContextAccessor
{
    public TenantContext? Current => store.Context;
}
