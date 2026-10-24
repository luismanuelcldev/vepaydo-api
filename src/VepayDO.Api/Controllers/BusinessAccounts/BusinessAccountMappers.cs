using VepayDO.Application.BusinessAccounts;
using VepayDO.Contracts.BusinessAccounts;

namespace VepayDO.Api.Controllers.BusinessAccounts;

/// <summary>
/// Maps the business-account application results onto the wire contracts, keeping
/// <c>VepayDO.Contracts</c> free of any Application dependency (AGENTS.md section 6).
/// </summary>
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
