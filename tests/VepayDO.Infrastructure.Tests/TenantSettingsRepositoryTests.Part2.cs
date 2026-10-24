using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Tenants;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class TenantSettingsRepositoryTests
{

    [Fact]
    public async Task Settings_are_isolated_between_tenants_under_rls()
    {
        var first = await SeedTenantAsync();
        var second = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var settings = new TenantSettingsRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(first.TenantId, first.OwnerId, DbRole.Owner),
            async ct =>
            {
                await settings.UpdateAsync(
                    first.TenantId,
                    TenantSettingsValues.Default.FromPrototype() with { DateWindowDays = 15 },
                    ct);
                return true;
            });

        var other = await host.Runner.RunAsync(
            TenantContext.ForTenant(second.TenantId, second.OwnerId, DbRole.Owner),
            ct => new ValueTask<TenantSettingsValues?>(settings.GetAsync(second.TenantId, ct)));

        other!.DateWindowDays.Should().Be(7);

        var misaddressed = await host.Runner.RunAsync(
            TenantContext.ForTenant(second.TenantId, second.OwnerId, DbRole.Owner),
            ct => new ValueTask<TenantSettingsValues?>(settings.GetAsync(first.TenantId, ct)));

        misaddressed.Should().BeNull();
    }

    private async Task<(Guid TenantId, Guid OwnerId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userId = await admin.InsertUserAsync($"settings-{suffix}@example.test", $"Settings {suffix}");
        var tenantId = await admin.CreateTenantAsync($"Settings Tenant {suffix}", userId);

        return (tenantId, userId);
    }
}
