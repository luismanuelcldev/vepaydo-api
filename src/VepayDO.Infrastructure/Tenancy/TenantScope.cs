using VepayDO.Application.Tenancy;
using VepayDO.Infrastructure.Persistence;

namespace VepayDO.Infrastructure.Tenancy;

internal sealed class TenantScope
{
    public TenantScope(TenantContext context, AppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(db);

        Context = context;
        Db = db;
    }

    public TenantContext Context { get; set; }

    public AppDbContext Db { get; }
}
