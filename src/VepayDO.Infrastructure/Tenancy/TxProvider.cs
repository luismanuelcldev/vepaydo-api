using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence;

namespace VepayDO.Infrastructure.Tenancy;

public sealed class TxProvider(TenantContextStore store)
{
    public AppDbContext DbContext => store.DbContext ?? throw new MissingTenantContextException();

    public TenantContext Context => store.Context ?? throw new MissingTenantContextException();
}
