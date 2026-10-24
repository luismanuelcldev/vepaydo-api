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

/// <summary>
/// Integration tests for <see cref="ChargeRepository"/> against a real PostgreSQL
/// instance (AGENTS.md sections 7 and 9.2): charges round-trip under RLS, the code
/// uniqueness lookup works, voiding stamps the row, balances come from
/// <c>v_charge_balances</c>, and reads are confined to the tenant.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ChargeRepositoryTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public ChargeRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_and_list_charges_round_trip_with_zero_balance_applied()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var id = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(
                tenant.TenantId,
                new NewCharge(null, "FACT-001", "Servicio", 1500m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
                ct)));

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<ChargeRecord>>(charges.ListAsync(tenant.TenantId, new ChargeQuery(null, null), ct)));

        list.Should().ContainSingle();
        list[0].Id.Should().Be(id);
        list[0].Code.Should().Be("FACT-001");
        list[0].Status.Should().Be("OPEN");
        list[0].AmountApplied.Should().Be(0m);
        list[0].Balance.Should().Be(1500m);
    }

    [Fact]
    public async Task Update_changes_the_mutable_fields_and_void_stamps_the_row()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var id = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(
                tenant.TenantId,
                new NewCharge(null, "FACT-001", "Original", 100m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
                ct)));

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await charges.UpdateAsync(
                    tenant.TenantId,
                    id,
                    new UpdateChargeFields(null, "FACT-002", "Editada", 250m, "USD", new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 9)),
                    ct);
                await charges.VoidAsync(tenant.TenantId, id, "Creada por error", tenant.OwnerId, ct);
                return true;
            });

        var record = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<ChargeRecord?>(charges.FindAsync(tenant.TenantId, id, ct)));

        record.Should().NotBeNull();
        record!.Code.Should().Be("FACT-002");
        record.Description.Should().Be("Editada");
        record.Amount.Should().Be(250m);
        record.Currency.Should().Be("USD");
        record.Status.Should().Be("VOIDED");
        record.VoidReason.Should().Be("Creada por error");
        record.VoidedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Code_exists_is_scoped_to_the_tenant_and_can_exclude_self()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var id = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(
                tenant.TenantId,
                new NewCharge(null, "FACT-001", "Servicio", 10m, "DOP", new DateOnly(2026, 3, 1), null, null),
                ct)));

        var exists = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<bool>(charges.CodeExistsAsync(tenant.TenantId, "FACT-001", null, ct)));

        var existsExcludingSelf = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<bool>(charges.CodeExistsAsync(tenant.TenantId, "FACT-001", id, ct)));

        exists.Should().BeTrue();
        existsExcludingSelf.Should().BeFalse();
    }

    [Fact]
    public async Task Balance_reflects_active_allocations_from_the_view()
    {
        var tenant = await SeedTenantAsync();

        Guid chargeId;
        await using (var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await admin.UseContextAsync(tenant.TenantId, tenant.OwnerId, "owner");
            chargeId = await admin.InsertChargeAsync("FACT-001", 1000m);
            var receiptId = await admin.InsertReceiptAsync(400m);
            await admin.ExecuteAsync(
                """
                INSERT INTO allocations (tenant_id, receipt_id, charge_id, amount_applied, created_by)
                VALUES (app.current_tenant_id(), $1, $2, 400, app.current_user_id())
                """, receiptId, chargeId);
            await admin.CommitAsync();
        }

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var record = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<ChargeRecord?>(charges.FindAsync(tenant.TenantId, chargeId, ct)));

        record.Should().NotBeNull();
        record!.Amount.Should().Be(1000m);
        record.AmountApplied.Should().Be(400m);
        record.Balance.Should().Be(600m);
    }

    [Fact]
    public async Task Add_many_inserts_all_rows_in_one_transaction()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var rows = new List<NewCharge>
        {
            new(null, "A-1", "Uno", 10m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
            new(null, "A-2", "Dos", 20m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
        };

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await charges.AddManyAsync(tenant.TenantId, rows, ct);
                return true;
            });

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<ChargeRecord>>(charges.ListAsync(tenant.TenantId, new ChargeQuery(null, null), ct)));

        list.Should().HaveCount(2);
        list.Select(c => c.Code).Should().BeEquivalentTo(["A-1", "A-2"]);
    }

    [Fact]
    public async Task A_duplicate_code_is_rejected_by_the_unique_index()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var charge = new NewCharge(
            null, "FACT-001", "Servicio", 100m, "DOP", new DateOnly(2026, 3, 1), null, null);

        await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(tenant.TenantId, charge, ct)));

        var act = async () => await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(tenant.TenantId, charge, ct)));

        await act.Should().ThrowAsync<ConflictException>();
    }

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
