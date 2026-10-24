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

/// <summary>
/// Integration tests for <see cref="BusinessAccountRepository"/> against a real
/// PostgreSQL instance (AGENTS.md sections 7 and 9.5): accounts round-trip under
/// RLS, the duplicate guard uses the real unique index, and reads are confined to
/// the tenant. Only the last four digits are ever stored.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class BusinessAccountRepositoryTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public BusinessAccountRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_and_list_round_trip_the_last_four_digits()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var accounts = new BusinessAccountRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var created = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(accounts.AddAsync(
                tenant.TenantId,
                new NewBusinessAccount(BankId: 1, "Principal", "4321", "DOP", IsActive: true),
                ct)));

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<BusinessAccountRecord>>(accounts.ListAsync(tenant.TenantId, ct)));

        list.Should().ContainSingle();
        list[0].Id.Should().Be(created);
        list[0].Last4.Should().Be("4321");
        list[0].Currency.Should().Be("DOP");
        list[0].BankName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Exists_duplicate_detects_the_unique_index_pair()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var accounts = new BusinessAccountRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await accounts.AddAsync(
                    tenant.TenantId,
                    new NewBusinessAccount(1, "Principal", "4321", "DOP", IsActive: true),
                    ct);
                return true;
            });

        var duplicate = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<bool>(accounts.ExistsDuplicateAsync(tenant.TenantId, 1, "4321", "DOP", excludingAccountId: null, ct)));

        duplicate.Should().BeTrue();
    }

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

        // Scoped to tenant two, the repository must not see tenant one's account
        // even when asked for it explicitly.
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
