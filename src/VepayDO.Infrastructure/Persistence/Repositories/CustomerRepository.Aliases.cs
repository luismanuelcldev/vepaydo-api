using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Customers;
using VepayDO.Application.Customers.Ports;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed partial class CustomerRepository
{
    public async Task<IReadOnlyList<string>> ListAliasesAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return await tx.DbContext.CustomerAliases
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CustomerId == customerId)
            .OrderBy(a => a.AliasNormalized)
            .Select(a => a.AliasNormalized)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CustomerAliasRecord?> FindAliasAsync(
        Guid tenantId,
        string aliasNormalized,
        CancellationToken cancellationToken = default)
    {
        var row = await tx.DbContext.CustomerAliases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.AliasNormalized == aliasNormalized,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : new CustomerAliasRecord(row.Id, row.CustomerId, row.AliasNormalized);
    }

    public async Task AddAliasAsync(
        Guid tenantId,
        Guid customerId,
        string aliasNormalized,
        CancellationToken cancellationToken = default)
    {
        tx.DbContext.CustomerAliases.Add(new CustomerAlias
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            AliasNormalized = aliasNormalized,
        });

        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAliasAsync(
        Guid tenantId,
        string aliasNormalized,
        CancellationToken cancellationToken = default)
    {
        await tx.DbContext.CustomerAliases
            .Where(a => a.TenantId == tenantId && a.AliasNormalized == aliasNormalized)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
