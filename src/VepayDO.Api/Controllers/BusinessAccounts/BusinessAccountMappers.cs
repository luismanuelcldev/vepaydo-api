using VepayDO.Application.BusinessAccounts;
using VepayDO.Contracts.BusinessAccounts;

namespace VepayDO.Api.Controllers.BusinessAccounts;

internal static class BusinessAccountMappers
{
    public static BusinessAccountResponse ToResponse(this BusinessAccountView account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new BusinessAccountResponse(
            account.Id,
            account.BankId,
            account.BankName,
            account.Label,
            account.Last4,
            account.Currency,
            account.IsActive,
            account.CreatedAt,
            account.UpdatedAt);
    }

    public static IReadOnlyList<BusinessAccountResponse> ToResponse(this IReadOnlyList<BusinessAccountView> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        return accounts.Select(ToResponse).ToList();
    }
}
