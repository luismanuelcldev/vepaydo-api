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

/// <summary>
/// Integration tests for <see cref="TenantSettingsRepository"/> against a real
/// PostgreSQL instance (AGENTS.md sections 7 and 9.6): the tenant's settings row
/// round-trips under RLS, updates are confined to the tenant, and the default
/// prototype row seeded by <c>app.create_tenant</c> is read back faithfully.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantSettingsRepositoryTests
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

        // Reading tenant two's settings while scoped to tenant one must fail closed
        // under the tenant_isolation policy, so the update never leaked.
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

    /// <summary>One AppDbContext + store + runner, mirroring a scoped HTTP request.</summary>
    private sealed class Host : IAsyncDisposable
    {
        private Host(NpgsqlDataSource dataSource, AppDbContext db, TenantContextStore store)
        {
            DataSource = dataSource;
            Db = db;
            Store = store;
            Runner = new TenantTransactionRunner(db, store);
            Provider = new TxProvider(store);
        }

        public NpgsqlDataSource DataSource { get; }

        public AppDbContext Db { get; }

        public TenantContextStore Store { get; }

        public TenantTransactionRunner Runner { get; }

        public TxProvider Provider { get; }

        public static Host Create(string connectionString)
        {
            var dataSource = AppDataSource.Build(connectionString);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
                .Options;

            return new Host(dataSource, new AppDbContext(options), new TenantContextStore());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await DataSource.DisposeAsync();
        }
    }
}