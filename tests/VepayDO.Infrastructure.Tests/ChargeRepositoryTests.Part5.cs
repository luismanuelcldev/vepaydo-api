using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class ChargeRepositoryTests
{

    [Fact]
    public async Task Charges_are_isolated_between_tenants_under_rls()
    {
        var first = await SeedTenantAsync();
        var second = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(first.TenantId, first.OwnerId, DbRole.Owner),
            ct => new ValueTask<Guid>(charges.AddAsync(
                first.TenantId,
                new NewCharge(null, "FACT-001", "Ghost", 10m, "DOP", new DateOnly(2026, 3, 1), null, null),
                ct)));

        var list = await host.Runner.RunAsync(
            TenantContext.ForTenant(second.TenantId, second.OwnerId, DbRole.Owner),
            ct => new ValueTask<IReadOnlyList<ChargeRecord>>(charges.ListAsync(second.TenantId, new ChargeQuery(null, null), ct)));

        list.Should().BeEmpty();
    }

    private async Task<(Guid TenantId, Guid OwnerId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userId = await admin.InsertUserAsync($"charges-{suffix}@example.test", $"Charges {suffix}");
        var tenantId = await admin.CreateTenantAsync($"Charges Tenant {suffix}", userId);

        return (tenantId, userId);
    }
}
