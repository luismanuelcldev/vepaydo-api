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

/// <summary>
/// Integration tests for <see cref="CustomerRepository"/> against a real
/// PostgreSQL instance (AGENTS.md sections 7 and 9.3): customers and their
/// aliases round-trip under RLS, alias lookup/persistence works, and reads are
/// confined to the tenant.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CustomerRepositoryTests
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
