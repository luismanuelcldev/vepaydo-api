using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Creates tenants through <c>app.create_tenant</c>, the only supported path
/// (the runtime role has no INSERT on <c>tenants</c>). Runs in the caller's
/// transaction so the user, the tenant, its settings, the owner membership and
/// the tenant-created audit row commit together (AGENTS.md sections 7 and 10).
/// </summary>
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
