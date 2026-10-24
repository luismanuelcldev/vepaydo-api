using VepayDO.Application.Auth;

namespace VepayDO.Application.BusinessAccounts;

public interface IBusinessAccountService
{

    Task<IReadOnlyList<BusinessAccountView>> ListAsync(CancellationToken cancellationToken = default);

    Task<BusinessAccountView> GetAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<BusinessAccountView> CreateAsync(
        CreateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<BusinessAccountView> UpdateAsync(
        Guid accountId,
        UpdateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
