using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeBusinessAccountService : IBusinessAccountService
{
    public IReadOnlyList<BusinessAccountView> Accounts { get; set; } = [];

    public Exception? Error { get; set; }

    public CreateBusinessAccountInput? LastCreate { get; private set; }

    public void Reset()
    {
        Accounts = [];
        Error = null;
        LastCreate = null;
    }

    public Task<IReadOnlyList<BusinessAccountView>> ListAsync(CancellationToken cancellationToken = default) =>
        Error is not null
            ? Task.FromException<IReadOnlyList<BusinessAccountView>>(Error)
            : Task.FromResult(Accounts);

    public Task<BusinessAccountView> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            return Task.FromException<BusinessAccountView>(Error);
        }

        var found = Accounts.FirstOrDefault(a => a.Id == accountId);
        return found is null
            ? Task.FromException<BusinessAccountView>(new VepayDO.Domain.Errors.NotFoundException("Business account not found"))
            : Task.FromResult(found);
    }

    public Task<BusinessAccountView> CreateAsync(
        CreateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        LastCreate = input;

        if (Error is not null)
        {
            return Task.FromException<BusinessAccountView>(Error);
        }

        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new BusinessAccountView(
            Guid.NewGuid(),
            input.BankId,
            "Banco Test",
            input.Label,
            input.Last4,
            input.Currency.ToUpperInvariant(),
            IsActive: true,
            now,
            now));
    }
}
