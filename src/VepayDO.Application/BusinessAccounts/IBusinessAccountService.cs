using VepayDO.Application.Auth;

namespace VepayDO.Application.BusinessAccounts;

/// <summary>
/// Business-account use cases (UC-SET-03). Every route is OWNER-only (enforced by
/// the HTTP policy); reads and writes run inside the request transaction so the
/// tenant always comes from the ambient context.
/// </summary>
public interface IBusinessAccountService
{
    /// <summary>Lists the current tenant's accounts.</summary>
    Task<IReadOnlyList<BusinessAccountView>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns one account or throws when it does not belong to the tenant.</summary>
    Task<BusinessAccountView> GetAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Creates an account and writes one audit event in the same transaction.</summary>
    Task<BusinessAccountView> CreateAsync(
        CreateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Updates label/active and writes one audit event in the same transaction.</summary>
    Task<BusinessAccountView> UpdateAsync(
        Guid accountId,
        UpdateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
