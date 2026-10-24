using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Customers;
using VepayDO.Application.Customers.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Customers;

public sealed partial class CustomerServiceTests
{

    [Fact]
    public async Task Add_alias_normalizes_and_audits()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));

        await harness.Service.AddAliasAsync(
            CustomerId,
            new AddAliasInput("  José  PeReZ "),
            Meta(),
            CancellationToken.None);

        harness.Repo.Aliases[CustomerId].Should().ContainSingle().Which.Should().Be("jose perez");
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Entity.Should().Be("customer_alias");
        harness.Audit.Events[0].Action.Should().Be("add");
    }

    [Fact]
    public async Task Add_alias_to_an_unknown_customer_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.AddAliasAsync(
            Guid.NewGuid(),
            new AddAliasInput("x"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Move_alias_moves_it_and_audits_both_customers()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Add(Sample(OtherCustomerId, "Otro"));
        harness.Repo.Aliases[OtherCustomerId] = ["maria p"];

        var view = await harness.Service.MoveAliasAsync(
            CustomerId,
            new MoveAliasInput(OtherCustomerId, "Maria P"),
            Meta(),
            CancellationToken.None);

        view.Aliases.Should().ContainSingle().Which.Should().Be("maria p");
        harness.Repo.Aliases[OtherCustomerId].Should().BeEmpty();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("move");
    }

    [Fact]
    public async Task Move_alias_rejects_a_source_that_does_not_own_it()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Add(Sample(OtherCustomerId, "Otro"));

        var act = async () => await harness.Service.MoveAliasAsync(
            CustomerId,
            new MoveAliasInput(OtherCustomerId, "ghost"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    private static RequestMeta Meta() => new(null, null, null);
}
