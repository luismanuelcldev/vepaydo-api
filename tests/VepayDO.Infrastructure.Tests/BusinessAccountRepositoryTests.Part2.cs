using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class BusinessAccountRepositoryTests
{

    [Fact]
    public async Task Accounts_are_isolated_between_tenants_under_rls()
    {
        var first = await SeedTenantAsync();
        var second = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var accounts = new BusinessAccountRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(first.TenantId, first.OwnerId, DbRole.Owner),
            async ct =>
            {
                await accounts.AddAsync(
                    first.TenantId,
                    new NewBusinessAccount(1, "Principal", "4321", "DOP", IsActive: true),
                    ct);
                return true;
            });

        var leaked = await host.Runner.RunAsync(
            TenantContext.ForTenant(second.TenantId, second.OwnerId, DbRole.Owner),
            async ct =>
            {
                var list = await accounts.ListAsync(second.TenantId, ct);
                return list.Count;
            });

        leaked.Should().Be(0);
    }

    private async Task<(Guid TenantId, Guid OwnerId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userId = await admin.InsertUserAsync($"accounts-{suffix}@example.test", $"Accounts {suffix}");
        var tenantId = await admin.CreateTenantAsync($"Accounts Tenant {suffix}", userId);

        return (tenantId, userId);
    }
}
