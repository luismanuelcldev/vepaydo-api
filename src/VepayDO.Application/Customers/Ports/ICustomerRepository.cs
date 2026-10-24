namespace VepayDO.Application.Customers.Ports;

using VepayDO.Application.Customers;

public interface ICustomerRepository
{

    Task<IReadOnlyList<CustomerRecord>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<CustomerRecord?> FindAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAliasesAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerAliasRecord?> FindAliasAsync(
        Guid tenantId,
        string aliasNormalized,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(
        Guid tenantId,
        CustomerRecord customer,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid tenantId,
        Guid customerId,
        string name,
        string nameNormalized,
        string? phone,
        string? notes,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task AddAliasAsync(
        Guid tenantId,
        Guid customerId,
        string aliasNormalized,
        CancellationToken cancellationToken = default);

    Task RemoveAliasAsync(
        Guid tenantId,
        string aliasNormalized,
        CancellationToken cancellationToken = default);
}
