using Microsoft.EntityFrameworkCore;

using VepayDO.Application.Customers;
using VepayDO.Application.Customers.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes the current tenant's <c>customers</c> and <c>customer_aliases</c>
/// rows under the request's tenant context. Both tables carry the simple
/// <c>tenant_isolation</c> RLS policy, so every query is confined to
/// <c>app.tenant_id</c>. Alias uniqueness is guaranteed by the
/// <c>(tenant_id, alias_normalized)</c> unique index; the repository surfaces the
/// lookup so the service can return a friendly error first (AGENTS.md section 7).
/// </summary>
public sealed class CustomerRepository(TxProvider tx, IClock clock) : ICustomerRepository
{
    public async Task<IReadOnlyList<CustomerRecord>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await tx.DbContext.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.NameNormalized)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<CustomerRecord?> FindAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var row = await tx.DbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToRecord(row);
    }

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

    public async Task<Guid> AddAsync(
        Guid tenantId,
        CustomerRecord customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var now = clock.UtcNow;
        var entity = new Customer
        {
            Id = customer.Id,
            TenantId = tenantId,
            Name = customer.Name,
            NameNormalized = customer.NameNormalized,
            Phone = customer.Phone,
            Notes = customer.Notes,
            IsActive = customer.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        tx.DbContext.Customers.Add(entity);
        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entity.Id;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid customerId,
        string name,
        string nameNormalized,
        string? phone,
        string? notes,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await tx.DbContext.Customers
            .Where(c => c.TenantId == tenantId && c.Id == customerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.Name, name)
                    .SetProperty(c => c.NameNormalized, nameNormalized)
                    .SetProperty(c => c.Phone, phone)
                    .SetProperty(c => c.Notes, notes)
                    .SetProperty(c => c.IsActive, isActive)
                    .SetProperty(c => c.UpdatedAt, clock.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
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

    private static CustomerRecord ToRecord(Customer customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.NameNormalized,
            customer.Phone,
            customer.Notes,
            customer.IsActive,
            customer.CreatedAt,
            customer.UpdatedAt);
}
