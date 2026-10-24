using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeBusinessAccountService
{

    public Task<BusinessAccountView> UpdateAsync(
        Guid accountId,
        UpdateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<BusinessAccountView>(Error);
        }

        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new BusinessAccountView(
            accountId,
            BankId: 1,
            BankName: "Banco Test",
            Label: input.Label ?? "Principal",
            Last4: "1234",
            Currency: "DOP",
            IsActive: input.IsActive ?? true,
            now,
            now));
    }
}
