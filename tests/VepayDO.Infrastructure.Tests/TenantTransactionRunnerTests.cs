using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Exercises the runtime tenant-transaction machinery (AGENTS.md section 7): the
/// context is installed with parameterized <c>set_config</c> calls, RLS applies to
/// every unit of work, the store isolates concurrent flows, and <see cref="TxProvider"/>
/// fails closed when no transaction is active.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantTransactionRunnerTests
{
    private readonly PostgresFixture _fixture;

    public TenantTransactionRunnerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_scopes_reads_and_writes_to_the_context_tenant()
    {
        var (tenantA, userA, tenantB, userB) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                await db.SaveChangesAsync(ct);
                return true;
            });

        host.Accessor.Current.Should().BeNull("the context must be cleared once the run ends");

        (await CanSeeReceiptAsync(receiptA)).Should()
            .BeFalse("a connection with no tenant context fails closed");

        var visibleInB = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
            async ct => await host.Provider.DbContext.Receipts.AsNoTracking()
                .AnyAsync(r => r.Id == receiptA, ct));

        visibleInB.Should().BeFalse("tenant B must not read tenant A rows");

        var visibleInA = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct => await host.Provider.DbContext.Receipts.AsNoTracking()
                .AnyAsync(r => r.Id == receiptA, ct));

        visibleInA.Should().BeTrue("the owning tenant reads its own row");
    }

    [Fact]
    public async Task A_following_run_starts_from_a_clean_context()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                await db.SaveChangesAsync(ct);
                return true;
            });

        var visible = await host.Runner.RunAsync(
            TenantContext.None,
            async ct => await host.Provider.DbContext.Receipts.AsNoTracking()
                .AnyAsync(r => r.Id == receiptA, ct));

        visible.Should().BeFalse("the previous tenant must not leak into the next run");
    }

    [Fact]
    public async Task Concurrent_flows_keep_their_own_context()
    {
        var (tenantA, userA, tenantB, userB) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();
        var receiptB = Guid.NewGuid();

        await using (var seedHost = Host.Create(_fixture.AppConnectionString))
        {
            await seedHost.Runner.RunAsync(
                TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
                async ct =>
                {
                    var db = seedHost.Provider.DbContext;
                    db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                    await db.SaveChangesAsync(ct);
                    return true;
                });

            await seedHost.Runner.RunAsync(
                TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
                async ct =>
                {
                    var db = seedHost.Provider.DbContext;
                    db.Receipts.Add(NewReceipt(tenantB, userB, receiptB));
                    await db.SaveChangesAsync(ct);
                    return true;
                });
        }

        // Two independent AppDbContext instances share one store, exactly like the
        // scoped DbContext / singleton accessor pair wired by the container.
        var store = new TenantContextStore();
        await using var hostA = Host.Create(_fixture.AppConnectionString, store);
        await using var hostB = Host.Create(_fixture.AppConnectionString, store);

        async Task<bool> Observe(Host host, TenantContext context, Guid own, Guid foreign)
            => await host.Runner.RunAsync(context, async ct =>
            {
                var db = host.Provider.DbContext;

                var ownVisible = await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == own, ct);
                var foreignVisible = await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == foreign, ct);

                return ownVisible && !foreignVisible;
            });

        var first = Task.Run(() => Observe(
            hostA,
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            receiptA,
            receiptB));

        var second = Task.Run(() => Observe(
            hostB,
            TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
            receiptB,
            receiptA));

        await Task.WhenAll(first, second);

        (await first).Should().BeTrue("flow A must see only tenant A");
        (await second).Should().BeTrue("flow B must see only tenant B");
    }

    [Fact]
    public void TxProvider_fails_closed_when_no_transaction_is_active()
    {
        var store = new TenantContextStore();
        var provider = new TxProvider(store);
        var accessor = new TenantContextAccessor(store);

        accessor.Current.Should().BeNull();

        Assert.Throws<MissingTenantContextException>(() => _ = provider.DbContext);
        Assert.Throws<MissingTenantContextException>(() => _ = provider.Context);
    }

    [Fact]
    public async Task RunAsUserAsync_reads_only_the_users_own_memberships_and_tenant()
    {
        var (tenantA, userA, _, userB) = await SeedTwoTenantsAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);

        var result = await host.Runner.RunAsUserAsync(userA, async ct =>
        {
            var db = host.Provider.DbContext;

            host.Provider.Context.TenantId.Should().BeNull("the pre-auth flow has no tenant");
            host.Provider.Context.UserId.Should().Be(userA);

            var membershipTenants = await db.Memberships.AsNoTracking()
                .Where(m => m.UserId == userA)
                .Select(m => m.TenantId)
                .ToListAsync(ct);

            var visibleTenants = await db.Tenants.AsNoTracking()
                .Select(t => t.Id)
                .ToListAsync(ct);

            var foreignMembership = await db.Memberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userB, ct);

            var anyBusinessRow = await db.Receipts.AsNoTracking().AnyAsync(ct);

            return (membershipTenants, visibleTenants, foreignMembership, anyBusinessRow);
        });

        result.membershipTenants.Should().ContainSingle().Which.Should().Be(tenantA);
        result.visibleTenants.Should().BeEquivalentTo([tenantA]);
        result.foreignMembership.Should().BeFalse();
        result.anyBusinessRow.Should()
            .BeFalse("tenant-scoped tables stay invisible until a tenant is set");
    }

    [Fact]
    public async Task An_empty_context_sees_no_rows_at_all()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);
        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                await db.SaveChangesAsync(ct);
                return true;
            });

        var seen = await host.Runner.RunAsync(TenantContext.None, async ct =>
        {
            var db = host.Provider.DbContext;
            var receipts = await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptA, ct);
            var memberships = await db.Memberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userA, ct);
            return (receipts, memberships);
        });

        seen.receipts.Should().BeFalse();
        seen.memberships.Should().BeFalse();
    }

    [Fact]
    public async Task A_critical_alert_is_untouchable_by_a_verifier_but_not_by_the_system_role()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptId = Guid.NewGuid();
        var alertId = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptId));
                db.Alerts.Add(new Alert
                {
                    Id = alertId,
                    TenantId = tenantA,
                    ReceiptId = receiptId,
                    Rule = AlertRuleCode.DUPLICATE_REFERENCE,
                    Severity = AlertSeverity.CRITICAL,
                    Message = "This reference was already used",
                    Details = "{}",
                    Status = AlertStatus.OPEN,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                return true;
            });

        var verifierRows = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Verifier),
            async ct => await host.Provider.DbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE alerts SET status = 'RESOLVED', resolution = 'REJECTED',
                                  resolved_by = {userA}, resolved_at = now(),
                                  resolution_reason = 'ignored by the test'
                WHERE id = {alertId}
                """,
                ct));

        verifierRows.Should().Be(0, "the alerts_update policy hides CRITICAL rows from a verifier");

        var systemRows = await host.Runner.RunAsSystemAsync(
            tenantA,
            async ct => await host.Provider.DbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE alerts SET status = 'RESOLVED', resolution = 'AUTO_CLEARED',
                                  resolved_at = now()
                WHERE id = {alertId}
                """,
                ct));

        systemRows.Should().Be(1, "the system role may re-evaluate alerts");

        var status = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct => await host.Provider.DbContext.Alerts.AsNoTracking()
                .Where(a => a.Id == alertId)
                .Select(a => a.Status)
                .SingleAsync(ct));

        status.Should().Be(AlertStatus.RESOLVED);
    }

    [Fact]
    public async Task The_audit_log_is_appended_by_any_role_but_read_only_by_the_owner()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);

        // INSERT without RETURNING: a verifier may append audit rows even though the
        // SELECT policy is owner-only (the RETURNING clause would need SELECT rights).
        var inserted = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Verifier),
            async ct => await host.Provider.DbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO audit_events (tenant_id, user_id, actor, entity, action, created_at)
                VALUES ({tenantA}, {userA}, 'USER', 'receipt', 'confirm', now())
                """,
                ct));

        inserted.Should().Be(1);

        var verifierReads = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Verifier),
            async ct => await host.Provider.DbContext.AuditEvents.AsNoTracking()
                .CountAsync(a => a.TenantId == tenantA && a.Action == "confirm", ct));

        verifierReads.Should().Be(0, "the audit SELECT policy is owner-only");

        var ownerReads = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct => await host.Provider.DbContext.AuditEvents.AsNoTracking()
                .CountAsync(a => a.TenantId == tenantA && a.Action == "confirm", ct));

        ownerReads.Should().Be(1);
    }

    [Fact]
    public async Task CreateTenantAsync_creates_the_tenant_and_its_owner_membership()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        var owner = await admin.InsertUserAsync($"create-{suffix}@example.test", "New owner");

        await using var host = Host.Create(_fixture.AppConnectionString);

        var tenantId = await host.Runner.CreateTenantAsync($"Created {suffix}", owner);

        tenantId.Should().NotBeEmpty();
        host.Accessor.Current.Should().BeNull();

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, owner, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;

                var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, ct);
                tenant.Status.Should().Be("active");

                var settings = await db.TenantSettings.AsNoTracking()
                    .CountAsync(s => s.TenantId == tenantId, ct);
                settings.Should().Be(1, "app.create_tenant seeds the defaults");

                var membership = await db.Memberships.AsNoTracking()
                    .SingleAsync(m => m.TenantId == tenantId, ct);
                membership.UserId.Should().Be(owner);
                membership.Role.Should().Be(MemberRole.OWNER);
                membership.IsActive.Should().BeTrue();

                return true;
            });
    }

    [Fact]
    public async Task A_throwing_unit_of_work_is_rolled_back()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptId = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        var act = async () => await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptId));
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("boom");
            });

        await act.Should().ThrowAsync<InvalidOperationException>();

        (await CanSeeReceiptAsync(receiptId)).Should().BeFalse();
    }

    [Fact]
    public async Task ActivateAsync_switches_the_tenant_inside_the_running_transaction()
    {
        var (tenantA, userA, tenantB, userB) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();
        var receiptB = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                await db.SaveChangesAsync(ct);
                return true;
            });

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantB, userB, receiptB));
                await db.SaveChangesAsync(ct);
                return true;
            });

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;

                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptA, ct))
                    .Should().BeTrue();
                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptB, ct))
                    .Should().BeFalse();

                await host.Runner.ActivateAsync(
                    TenantContext.ForTenant(tenantB, userB, DbRole.Owner), ct);

                host.Accessor.Current!.TenantId.Should().Be(tenantB);

                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptB, ct))
                    .Should().BeTrue();
                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptA, ct))
                    .Should().BeFalse();

                return true;
            });
    }

    private async Task<bool> CanSeeReceiptAsync(Guid receiptId)
    {
        await using var dataSource = AppDataSource.Build(_fixture.AppConnectionString);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
            .Options;

        await using var db = new AppDbContext(options);
        return await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptId);
    }

    private async Task<(Guid TenantA, Guid UserA, Guid TenantB, Guid UserB)> SeedTwoTenantsAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userA = await admin.InsertUserAsync($"runner-a-{suffix}@example.test", "Runner A");
        var userB = await admin.InsertUserAsync($"runner-b-{suffix}@example.test", "Runner B");

        var tenantA = await admin.CreateTenantAsync($"Runner Tenant A {suffix}", userA);
        var tenantB = await admin.CreateTenantAsync($"Runner Tenant B {suffix}", userB);

        return (tenantA, userA, tenantB, userB);
    }

    private static Receipt NewReceipt(Guid tenantId, Guid uploadedBy, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        IdempotencyKey = Guid.NewGuid(),
        UploadedBy = uploadedBy,
        Source = ReceiptSource.WEB_UPLOAD,
        StorageKey = $"tenant/{tenantId}/receipts/{id}",
        Mime = "image/png",
        Bytes = 10,
        Sha256 = new byte[32],
        ProcessingStatus = ProcessingStatus.RECEIVED,
        ReconciliationStatus = ReconciliationStatus.PENDING_REVIEW,
        TransferType = TransferType.UNKNOWN,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

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
            Accessor = new TenantContextAccessor(store);
        }

        public NpgsqlDataSource DataSource { get; }

        public AppDbContext Db { get; }

        public TenantContextStore Store { get; }

        public TenantTransactionRunner Runner { get; }

        public TxProvider Provider { get; }

        public TenantContextAccessor Accessor { get; }

        public static Host Create(string connectionString, TenantContextStore? sharedStore = null)
        {
            var dataSource = AppDataSource.Build(connectionString);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
                .Options;

            return new Host(
                dataSource,
                new AppDbContext(options),
                sharedStore ?? new TenantContextStore());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await DataSource.DisposeAsync();
        }
    }
}
