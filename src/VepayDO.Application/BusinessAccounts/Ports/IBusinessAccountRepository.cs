namespace VepayDO.Application.BusinessAccounts.Ports;

using VepayDO.Application.BusinessAccounts;

/// <summary>
/// Reads and writes the current tenant's <c>business_accounts</c> rows under the
/// request's tenant context (RLS <c>tenant_isolation</c>). Only the last four
/// digits are ever stored or returned (AGENTS.md section 7).
/// </summary>
public interface IBusinessAccountRepository
{
    /// <summary>Lists the tenant's accounts, active first then by label.</summary>
    Task<IReadOnlyList<BusinessAccountRecord>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one account of the tenant, or <c>null</c> when it is not theirs.</summary>
    Task<BusinessAccountRecord?> FindAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    /// <summary>True when another account already uses the same bank/last4/currency.</summary>
    Task<bool> ExistsDuplicateAsync(
        Guid tenantId,
        short bankId,
        string last4,
        string currency,
        Guid? excludingAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts an account and returns its generated id.</summary>
    Task<Guid> AddAsync(
        Guid tenantId,
        NewBusinessAccount account,
        CancellationToken cancellationToken = default);

    /// <summary>Updates label/active for an existing account of the tenant.</summary>
    Task UpdateAsync(
        Guid tenantId,
        Guid accountId,
        string label,
        bool isActive,
        CancellationToken cancellationToken = default);
}
