using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.BusinessAccounts;

public sealed partial class BusinessAccountServiceTests
{

    [Fact]
    public async Task Update_of_an_unknown_account_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateBusinessAccountInput("X", null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static RequestMeta Meta() => new(null, null, null);

    private static BusinessAccountRecord Sample() =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            BankId: 1,
            BankName: "Banco Test",
            Label: "Principal",
            Last4: "1234",
            Currency: "DOP",
            IsActive: true,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Accounts = new FakeBusinessAccountRepository();
            Banks = new FakeBankRepository();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, UserId, DbRole.Owner);
            Service = new BusinessAccountService(
                Accounts,
                Banks,
                Audit,
                new FixedClock(DateTimeOffset.Parse("2026-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
                new FakeTenantContextAccessor(ctx));
        }

        public FakeBusinessAccountRepository Accounts { get; }

        public FakeBankRepository Banks { get; }

        public RecordingAuditWriter Audit { get; }

        public BusinessAccountService Service { get; }
    }
}
