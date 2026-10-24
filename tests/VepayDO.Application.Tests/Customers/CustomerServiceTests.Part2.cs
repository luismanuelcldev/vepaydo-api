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
    public async Task Update_can_rename_and_deactivate()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "Old Name"));

        var view = await harness.Service.UpdateAsync(
            CustomerId,
            new UpdateCustomerInput("Nuño Álvarez", null, null, IsActive: false),
            Meta(),
            CancellationToken.None);

        view.Name.Should().Be("Nuño Álvarez");
        view.NameNormalized.Should().Be("nuno alvarez");
        view.IsActive.Should().BeFalse();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("update");
    }

    [Fact]
    public async Task Add_alias_is_idempotent_for_the_same_customer()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Aliases[CustomerId] = ["maria p"];

        var view = await harness.Service.AddAliasAsync(
            CustomerId,
            new AddAliasInput("  María P "),
            Meta(),
            CancellationToken.None);

        view.Aliases.Should().ContainSingle();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_alias_of_another_customer_is_a_conflict()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Add(Sample(OtherCustomerId, "Otro"));
        harness.Repo.Aliases[OtherCustomerId] = ["maria p"];

        var act = async () => await harness.Service.AddAliasAsync(
            CustomerId,
            new AddAliasInput("Maria P"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        harness.Audit.Events.Should().BeEmpty();
    }
}
