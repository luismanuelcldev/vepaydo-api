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
    public async Task Create_rejects_a_malformed_last4()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", "12ab", "DOP"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_bank_last4_currency()
    {
        var harness = new Harness();
        harness.Accounts.Duplicate = true;

        var act = async () => await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", "4321", "DOP"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_writes_exactly_one_audit_event()
    {
        var harness = new Harness();

        await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", "4321", "DOP"),
            Meta(),
            CancellationToken.None);

        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Entity.Should().Be("business_account");
        harness.Audit.Events[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task Update_deactivates_and_audits()
    {
        var harness = new Harness();
        var account = Sample();
        harness.Accounts.Items.Add(account);

        var view = await harness.Service.UpdateAsync(
            account.Id,
            new UpdateBusinessAccountInput(null, IsActive: false),
            Meta(),
            CancellationToken.None);

        view.IsActive.Should().BeFalse();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("update");
    }
}
