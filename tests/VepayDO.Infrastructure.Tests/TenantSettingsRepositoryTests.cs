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

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class TenantSettingsRepositoryTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public TenantSettingsRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_reads_the_defaults_seeded_for_a_new_tenant()
    {
        var tenantId = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var settings = new TenantSettingsRepository(host.Provider, Clock);

        var values = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId.TenantId, tenantId.OwnerId, DbRole.Owner),
            ct => new ValueTask<TenantSettingsValues?>(settings.GetAsync(tenantId.TenantId, ct)));

        values.Should().NotBeNull();
        values!.DateWindowDays.Should().Be(7);
        values.AmountTolerance.Should().Be(0m);
        values.VerifierAmountCap.Should().BeNull();
        values.ImageRetentionMonths.Should().Be(24);
    }

    [Fact]
    public async Task Update_persists_every_field_and_round_trips()
    {
        var tenantId = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var settings = new TenantSettingsRepository(host.Provider, Clock);

        var context = TenantContext.ForTenant(tenantId.TenantId, tenantId.OwnerId, DbRole.Owner);

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await settings.UpdateAsync(
                    tenantId.TenantId,
                    TenantSettingsValues.Default.FromPrototype() with
                    {
                        DateWindowDays = 30,
                        AmountTolerance = 1.25m,
                        VerifierAmountCap = 5000m,
                        ImageRetentionMonths = 6,
                    },
                    ct);
                return true;
            });

        var read = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<TenantSettingsValues?>(settings.GetAsync(tenantId.TenantId, ct)));

        read!.DateWindowDays.Should().Be(30);
        read.AmountTolerance.Should().Be(1.25m);
        read.VerifierAmountCap.Should().Be(5000m);
        read.ImageRetentionMonths.Should().Be(6);
    }
}
