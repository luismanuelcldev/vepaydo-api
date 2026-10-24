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

public sealed partial class CustomerRepositoryTests
{

    [Fact]
    public async Task Aliases_round_trip_and_are_found_by_normalized_form()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var customers = new CustomerRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var now = Clock.UtcNow;
        var customerId = Guid.NewGuid();

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await customers.AddAsync(
                    tenant.TenantId,
                    new CustomerRecord(customerId, "José", "jose", null, null, true, now, now),
                    ct);
                await customers.AddAliasAsync(tenant.TenantId, customerId, "jose p", ct);
                return true;
            });

        var found = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<CustomerAliasRecord?>(customers.FindAliasAsync(tenant.TenantId, "jose p", ct)));

        found.Should().NotBeNull();
        found!.CustomerId.Should().Be(customerId);

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await customers.RemoveAliasAsync(tenant.TenantId, "jose p", ct);
                return true;
            });

        var aliases = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<string>>(customers.ListAliasesAsync(tenant.TenantId, customerId, ct)));

        aliases.Should().BeEmpty();
    }
}
