using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class TenantFactory(TxProvider tx) : ITenantFactory
{
    public async Task<Guid> CreateAsync(
        string name,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var ids = await tx.DbContext.Database
            .SqlQuery<Guid>($"""
                SELECT app.create_tenant({name}::text, {ownerUserId}::uuid) AS "tenant_id"
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ids.Count != 1 || ids[0] == Guid.Empty)
        {
            throw new InvalidOperationException("app.create_tenant did not return a tenant id");
        }

        return ids[0];
    }
}
