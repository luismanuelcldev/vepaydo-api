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
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherCustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task List_returns_customers_with_their_aliases()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María Pérez"));
        harness.Repo.Aliases[CustomerId] = ["maria perez"];

        var result = await harness.Service.ListAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("María Pérez");
        result[0].Aliases.Should().ContainSingle().Which.Should().Be("maria perez");
    }

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Create_normalizes_the_name()
    {
        var harness = new Harness();

        var view = await harness.Service.CreateAsync(
            new CreateCustomerInput("  MARÍA   Pérez ", null, "nota"),
            Meta(),
            CancellationToken.None);

        view.Name.Should().Be("MARÍA   Pérez");
        view.NameNormalized.Should().Be("maria perez");
        view.Notes.Should().Be("nota");
        view.IsActive.Should().BeTrue();
        harness.Audit.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_rejects_an_empty_name()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateCustomerInput("   ", null, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }
}
