using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Customers;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class CustomerRepositoryTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public CustomerRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_update_and_list_customers_round_trip()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var customers = new CustomerRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var now = Clock.UtcNow;
        var record = new CustomerRecord(
            Guid.NewGuid(), "María Pérez", "maria perez", "+1809", "nota", true, now, now);

        var created = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(customers.AddAsync(tenant.TenantId, record, ct)));

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await customers.UpdateAsync(
                    tenant.TenantId, created, "Nuño", "nuno", null, null, false, ct);
                return true;
            });

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<CustomerRecord>>(customers.ListAsync(tenant.TenantId, ct)));

        list.Should().ContainSingle();
        list[0].Name.Should().Be("Nuño");
        list[0].NameNormalized.Should().Be("nuno");
        list[0].IsActive.Should().BeFalse();
    }
}
