namespace VepayDO.Application.BusinessAccounts.Ports;

using VepayDO.Application.BusinessAccounts;

public interface IBusinessAccountRepository
{

    Task<IReadOnlyList<BusinessAccountRecord>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<BusinessAccountRecord?> FindAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsDuplicateAsync(
        Guid tenantId,
        short bankId,
        string last4,
        string currency,
        Guid? excludingAccountId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(
        Guid tenantId,
        NewBusinessAccount account,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid tenantId,
        Guid accountId,
        string label,
        bool isActive,
        CancellationToken cancellationToken = default);
}
