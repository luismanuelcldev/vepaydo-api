namespace VepayDO.Application.Customers.Ports;

using VepayDO.Application.Customers;

/// <summary>
/// Reads and writes the current tenant's <c>customers</c> and <c>customer_aliases</c>
/// rows under the request's tenant context (RLS <c>tenant_isolation</c>, AGENTS.md
/// section 7). Alias uniqueness is enforced by the <c>(tenant_id, alias_normalized)</c>
/// unique index; the repository also exposes a lookup so the service can surface a
/// controlled domain error before the constraint is hit.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>Lists the tenant's customers ordered by name.</summary>
    Task<IReadOnlyList<CustomerRecord>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one customer of the tenant, or <c>null</c> when it is not theirs.</summary>
    Task<CustomerRecord?> FindAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the aliases attached to the given customer.</summary>
    Task<IReadOnlyList<string>> ListAliasesAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds the alias row with the normalized form, regardless of owner.</summary>
    Task<CustomerAliasRecord?> FindAliasAsync(
        Guid tenantId,
        string aliasNormalized,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a customer and returns its generated id.</summary>
    Task<Guid> AddAsync(
        Guid tenantId,
        CustomerRecord customer,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the mutable fields of a customer.</summary>
    Task UpdateAsync(
        Guid tenantId,
        Guid customerId,
        string name,
        string nameNormalized,
        string? phone,
        string? notes,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts an alias row (normalized form) for a customer.</summary>
    Task AddAliasAsync(
        Guid tenantId,
        Guid customerId,
        string aliasNormalized,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an alias row by its normalized form in the tenant.</summary>
    Task RemoveAliasAsync(
        Guid tenantId,
        string aliasNormalized,
        CancellationToken cancellationToken = default);
}
