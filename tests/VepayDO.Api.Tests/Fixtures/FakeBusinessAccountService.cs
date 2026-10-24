using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;

namespace VepayDO.Api.Tests.Fixtures;

/// <summary>
/// Configuration-driven stand-in for <see cref="IBusinessAccountService"/>. The
/// real service needs PostgreSQL; these tests exercise the HTTP contract (verbs,
/// status codes, permission gating, validation, last4-only shape) around it.
/// </summary>
public sealed class FakeBusinessAccountService : IBusinessAccountService
{
    public IReadOnlyList<BusinessAccountView> Accounts { get; set; } = [];

    /// <summary>When set, every call throws instead of returning a result.</summary>
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
