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
    public async Task Customers_are_isolated_between_tenants_under_rls()
    {
        var first = await SeedTenantAsync();
        var second = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var customers = new CustomerRepository(host.Provider, Clock);

        var now = Clock.UtcNow;
        await host.Runner.RunAsync(
            TenantContext.ForTenant(first.TenantId, first.OwnerId, DbRole.Owner),
            async ct =>
            {
                await customers.AddAsync(
                    first.TenantId,
                    new CustomerRecord(Guid.NewGuid(), "Ghost", "ghost", null, null, true, now, now),
                    ct);
                return true;
            });

        var list = await host.Runner.RunAsync(
            TenantContext.ForTenant(second.TenantId, second.OwnerId, DbRole.Owner),
            ct => new ValueTask<IReadOnlyList<CustomerRecord>>(customers.ListAsync(second.TenantId, ct)));

        list.Should().BeEmpty();
    }

    private async Task<(Guid TenantId, Guid OwnerId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userId = await admin.InsertUserAsync($"customers-{suffix}@example.test", $"Customers {suffix}");
        var tenantId = await admin.CreateTenantAsync($"Customers Tenant {suffix}", userId);

        return (tenantId, userId);
    }
}
