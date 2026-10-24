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
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task List_returns_the_tenants_accounts()
    {
        var harness = new Harness();
        harness.Accounts.Items.Add(Sample());

        var result = await harness.Service.ListAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Last4.Should().Be("1234");
    }

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Get_of_an_unknown_account_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_stores_only_the_last_four_digits()
    {
        var harness = new Harness();

        var view = await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", " 4321 ", "dop"),
            Meta(),
            CancellationToken.None);

        view.Last4.Should().Be("4321");
        view.Currency.Should().Be("DOP");
        view.BankName.Should().Be("Banco Test");
        harness.Accounts.Added!.Last4.Should().Be("4321");
        view.Last4.Should().NotContain("-");
    }

    [Fact]
    public async Task Create_rejects_an_unknown_bank()
    {
        var harness = new Harness();
        harness.Banks.Known = null;

        var act = async () => await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(99, "Principal", "4321", "DOP"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
        harness.Audit.Events.Should().BeEmpty();
    }
}
