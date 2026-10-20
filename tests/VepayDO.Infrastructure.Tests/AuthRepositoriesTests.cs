using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Integration tests for the auth persistence layer against a real PostgreSQL
/// instance (AGENTS.md sections 7, 9.9 and 14): the global tables (users,
/// sessions, password_reset_tokens) round-trip without any tenant context, the
/// tenant-scoped tables (memberships) honour RLS, audit rows stay append-only,
/// and <see cref="TenantFactory"/> runs the real <c>app.create_tenant</c>.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuthRepositoriesTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public AuthRepositoriesTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UserRepository_round_trips_a_user_and_its_mutations()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"user-{suffix}@example.test";

        await using var host = Host.Create(_fixture.AppConnectionString);
        var users = new UserRepository(host.Provider, Clock);

        var id = Guid.NewGuid();
        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.CreateAsync(
                    new NewUser(id, email, "Ana Pérez", "hash:v1", "es-DO", MustChangePassword: true),
                    ct);
                return true;
            });

        var created = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByEmailAsync(email, ct)));

        created.Should().NotBeNull();
        created!.Id.Should().Be(id);
        created.Name.Should().Be("Ana Pérez");
        created.PasswordHash.Should().Be("hash:v1");
        created.IsActive.Should().BeTrue();
        created.PreferredLocale.Should().Be("es-DO");
        created.MustChangePassword.Should().BeTrue();
        created.IsPlatformSupport.Should().BeFalse();

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.UpdatePasswordAsync(id, "hash:v2", mustChangePassword: false, ct);
                await users.UpdateLocaleAsync(id, "en", ct);
                await users.UpdateLastLoginAsync(id, Clock.UtcNow, ct);
                await users.SetPlatformSupportAsync(id, isPlatformSupport: true, ct);
                return true;
            });

        var byId = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByIdAsync(id, ct)));

        byId.Should().NotBeNull();
        byId!.PasswordHash.Should().Be("hash:v2");
        byId.PreferredLocale.Should().Be("en");
        byId.MustChangePassword.Should().BeFalse();
        byId.IsPlatformSupport.Should().BeTrue();

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.SetActiveAsync(id, isActive: false, ct);
                return true;
            });

        var deactivated = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByIdAsync(id, ct)));

        deactivated!.IsActive.Should().BeFalse();

        var missing = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByEmailAsync($"absent-{suffix}@example.test", ct)));

        missing.Should().BeNull();
    }

    [Fact]
    public async Task UserRepository_matches_the_email_case_insensitively()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"Mixed-{suffix}@Example.Test";

        await using var host = Host.Create(_fixture.AppConnectionString);
        var users = new UserRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.CreateAsync(
                    new NewUser(Guid.NewGuid(), email, "Case", "hash:1", "es-DO", false),
                    ct);
                return true;
            });

        var found = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByEmailAsync($"MIXED-{suffix}@example.TEST", ct)));

        found.Should().NotBeNull("users.email is citext");
    }

    [Fact]
    public async Task SessionRepository_creates_reads_and_revokes_refresh_sessions()
    {
        var (tenantId, userId) = await SeedTenantAsync();
        var hash = RandomHash();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var sessions = new SessionRepository(host.Provider);

        var sessionId = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<Guid>(sessions.CreateAsync(
                new NewSession(
                    userId,
                    tenantId,
                    hash,
                    ExpiresAt: Clock.UtcNow.AddDays(30),
                    UserAgent: "Mozilla/5.0",
                    DeviceLabel: "Pixel 8",
                    Ip: IPAddress.Parse("190.80.1.2"),
                    CreatedAt: Clock.UtcNow),
                ct)));

        sessionId.Should().NotBeEmpty();

        var found = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindByTokenHashAsync(hash, ct)));

        found.Should().NotBeNull();
        found!.UserId.Should().Be(userId);
        found.TenantId.Should().Be(tenantId);
        found.DeviceLabel.Should().Be("Pixel 8");
        found.Ip.Should().Be(IPAddress.Parse("190.80.1.2"));
        found.RevokedAt.Should().BeNull();

        var active = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, userId, ct)));

        active.Should().NotBeNull();

        var activeForForeignUser = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, Guid.NewGuid(), ct)));

        activeForForeignUser.Should().BeNull("a session belongs to exactly one user");

        var listed = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<SessionRecord>>(
                sessions.ListActiveAsync(userId, Clock.UtcNow, ct)));

        listed.Should().ContainSingle(s => s.Id == sessionId);

        var revokedForeign = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<bool>(sessions.RevokeOwnedAsync(sessionId, Guid.NewGuid(), Clock.UtcNow, ct)));

        revokedForeign.Should().BeFalse("only the owner may revoke the session");

        var revoked = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<bool>(sessions.RevokeOwnedAsync(sessionId, userId, Clock.UtcNow, ct)));

        revoked.Should().BeTrue();

        var gone = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, userId, ct)));

        gone.Should().BeNull("the session is no longer active");
    }

    [Fact]
    public async Task SessionRepository_revokes_every_active_session_but_keeps_the_named_one()
    {
        var (tenantId, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var sessions = new SessionRepository(host.Provider);

        async Task<Guid> CreateAsync()
            => await host.Runner.RunAsync(
                TenantContext.None,
                ct => new ValueTask<Guid>(sessions.CreateAsync(
                    new NewSession(
                        userId,
                        tenantId,
                        RandomHash(),
                        Clock.UtcNow.AddDays(30),
                        null,
                        null,
                        null,
                        Clock.UtcNow),
                    ct)));

        var keep = await CreateAsync();
        await CreateAsync();
        await CreateAsync();

        var revokedIds = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<Guid>>(
                sessions.RevokeAllExceptAsync(userId, keep, Clock.UtcNow, ct)));

        revokedIds.Should().HaveCount(2);

        var stillActive = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<SessionRecord>>(
                sessions.ListActiveAsync(userId, Clock.UtcNow, ct)));

        stillActive.Should().ContainSingle().Which.Id.Should().Be(keep);

        var afterAll = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<Guid>>(
                sessions.RevokeAllForUserAsync(userId, Clock.UtcNow, ct)));

        afterAll.Should().ContainSingle().Which.Should().Be(keep);

        var noneActive = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<SessionRecord>>(
                sessions.ListActiveAsync(userId, Clock.UtcNow, ct)));

        noneActive.Should().BeEmpty();
    }

    [Fact]
    public async Task SessionRepository_binds_the_chosen_tenant_to_the_session()
    {
        var (tenantId, userId) = await SeedTenantAsync();
        var hash = RandomHash();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var sessions = new SessionRepository(host.Provider);

        await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<Guid>(sessions.CreateAsync(
                new NewSession(userId, null, hash, Clock.UtcNow.AddDays(30), null, null, null, Clock.UtcNow),
                ct)));

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await sessions.BindTenantAsync(hash, userId, tenantId, Clock.UtcNow, ct);
                return true;
            });

        var bound = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, userId, ct)));

        bound!.TenantId.Should().Be(tenantId);
        bound.LastSeenAt.Should().Be(Clock.UtcNow);
    }

    [Fact]
    public async Task PasswordResetTokenRepository_stores_only_the_hash_and_is_single_use()
    {
        var (_, userId) = await SeedTenantAsync();
        var hash = RandomHash();
        var future = Clock.UtcNow.AddMinutes(30);

        await using var host = Host.Create(_fixture.AppConnectionString);
        var tokens = new PasswordResetTokenRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await tokens.CreateAsync(
                    new NewPasswordResetToken(userId, hash, future, IPAddress.Parse("10.0.0.9"), null),
                    ct);
                return true;
            });

        var found = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<PasswordResetTokenRecord?>(tokens.FindByTokenHashAsync(hash, ct)));

        found.Should().NotBeNull();
        found!.UserId.Should().Be(userId);
        found.UsedAt.Should().BeNull();
        found.ExpiresAt.Should().Be(future);

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await tokens.MarkUsedAsync(found.Id, Clock.UtcNow, ct);
                return true;
            });

        var used = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<PasswordResetTokenRecord?>(tokens.FindByTokenHashAsync(hash, ct)));

        used!.UsedAt.Should().Be(Clock.UtcNow);

        var rawHash = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<string>(
                host.Provider.DbContext.PasswordResetTokens.AsNoTracking()
                    .Where(t => t.Id == found.Id)
                    .Select(t => t.RequestedIp!.ToString())
                    .SingleAsync(ct)));

        rawHash.Should().Be("10.0.0.9");
    }

    [Fact]
    public async Task PasswordResetTokenRepository_invalidates_every_unused_token_for_the_user()
    {
        var (_, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var tokens = new PasswordResetTokenRepository(host.Provider, Clock);

        for (var i = 0; i < 3; i++)
        {
            await host.Runner.RunAsync(
                TenantContext.None,
                async ct =>
                {
                    await tokens.CreateAsync(
                        new NewPasswordResetToken(
                            userId,
                            RandomHash(),
                            Clock.UtcNow.AddMinutes(30),
                            null,
                            null),
                        ct);
                    return true;
                });
        }

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await tokens.InvalidateUnusedForUserAsync(userId, Clock.UtcNow, ct);
                return true;
            });

        var unused = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<int>(
                host.Provider.DbContext.PasswordResetTokens.AsNoTracking()
                    .CountAsync(t => t.UserId == userId && t.UsedAt == null, ct)));

        unused.Should().Be(0);
    }

    [Fact]
    public async Task MembershipRepository_reads_the_users_own_memberships_under_rls()
    {
        var (tenantA, userA) = await SeedTenantAsync();
        var (tenantB, userB) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var memberships = new MembershipRepository(host.Provider, Clock);

        var own = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListActiveByUserAsync(userA, ct)));

        own.Should().ContainSingle();
        own[0].TenantId.Should().Be(tenantA);
        own[0].Role.Should().Be(MemberRole.OWNER);
        own[0].TenantName.Should().NotBeNullOrWhiteSpace();

        var foreign = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListActiveByUserAsync(userB, ct)));

        foreign.Should().BeEmpty("a user sees only its own memberships");

        var foreignTenant = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<MembershipRecord?>(memberships.FindAsync(tenantB, userA, ct)));

        foreignTenant.Should().BeNull("RLS hides tenant B from user A");

        var ownTenant = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<MembershipRecord?>(memberships.FindAsync(tenantA, userA, ct)));

        ownTenant.Should().NotBeNull();
        ownTenant!.Role.Should().Be(MemberRole.OWNER);
    }

    [Fact]
    public async Task MembershipRepository_lists_and_updates_members_within_the_tenant()
    {
        var (tenantId, ownerId) = await SeedTenantAsync();
        var verifierId = await InsertUserAsync("Verifier");

        await using var host = Host.Create(_fixture.AppConnectionString);
        var memberships = new MembershipRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                await memberships.AddAsync(tenantId, verifierId, MemberRole.VERIFIER, ct);
                return true;
            });

        var listed = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListByTenantAsync(tenantId, ct)));

        listed.Should().HaveCount(2);
        var verifier = listed.Should().ContainSingle(m => m.UserId == verifierId).Which;
        verifier.Role.Should().Be(MemberRole.VERIFIER);
        verifier.UserEmail.Should().NotBeNullOrWhiteSpace();

        var ownerCount = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<int>(memberships.CountActiveOwnersAsync(tenantId, ct)));

        ownerCount.Should().Be(1);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                await memberships.UpdateRoleAsync(tenantId, verifierId, MemberRole.OWNER, ct);
                return true;
            });

        var owners = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<int>(memberships.CountActiveOwnersAsync(tenantId, ct)));

        owners.Should().Be(2);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                await memberships.SetActiveAsync(tenantId, verifierId, isActive: false, ct);
                return true;
            });

        var active = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<int>(memberships.CountActiveOwnersAsync(tenantId, ct)));

        active.Should().Be(1, "the deactivated member no longer counts as an active owner");
    }

    [Fact]
    public async Task MembershipRepository_hides_members_of_another_tenant()
    {
        var (tenantA, ownerA) = await SeedTenantAsync();
        var (tenantB, ownerB) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var memberships = new MembershipRepository(host.Provider, Clock);

        var crossRead = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantB, ownerB, DbRole.Owner),
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListByTenantAsync(tenantA, ct)));

        crossRead.Should().BeEmpty("RLS keeps tenant A rows out of tenant B's scope");
    }

    [Fact]
    public async Task AuditEventRepository_writes_append_only_rows_visible_to_the_owner()
    {
        var (tenantId, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var audit = new AuditEventRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await audit.WriteAsync(
                    new AuditEventDraft
                    {
                        TenantId = tenantId,
                        UserId = userId,
                        Actor = AuditActor.USER,
                        Entity = "receipt",
                        EntityId = Guid.NewGuid(),
                        Action = "confirm",
                        Before = """{"status":"PENDING_REVIEW"}""",
                        After = """{"status":"CONFIRMED"}""",
                        Reason = null,
                        Ip = IPAddress.Parse("190.80.1.2"),
                    },
                    ct);
                return true;
            });

        var rows = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            ct => new ValueTask<int>(
                host.Provider.DbContext.AuditEvents.AsNoTracking()
                    .CountAsync(a => a.TenantId == tenantId && a.Action == "confirm", ct)));

        rows.Should().Be(1);

        var hiddenFromVerifier = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Verifier),
            ct => new ValueTask<int>(
                host.Provider.DbContext.AuditEvents.AsNoTracking()
                    .CountAsync(a => a.TenantId == tenantId && a.Action == "confirm", ct)));

        hiddenFromVerifier.Should().Be(0, "the audit SELECT policy is owner-only");
    }

    [Fact]
    public async Task Audit_events_reject_updates_and_deletes_for_the_runtime_role()
    {
        var (tenantId, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var audit = new AuditEventRepository(host.Provider, Clock);

        var entityId = Guid.NewGuid();
        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await audit.WriteAsync(
                    new AuditEventDraft
                    {
                        TenantId = tenantId,
                        UserId = userId,
                        Actor = AuditActor.USER,
                        Entity = "receipt",
                        EntityId = entityId,
                        Action = "reject",
                    },
                    ct);
                return true;
            });

        var updateAttempt = async () => await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await host.Provider.DbContext.Database.ExecuteSqlAsync(
                    $"UPDATE audit_events SET action = 'tampered' WHERE entity_id = {entityId}",
                    ct);
                return true;
            });

        await updateAttempt.Should().ThrowAsync<PostgresException>(
            "the immutability trigger rejects audit updates");

        var deleteAttempt = async () => await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await host.Provider.DbContext.Database.ExecuteSqlAsync(
                    $"DELETE FROM audit_events WHERE entity_id = {entityId}",
                    ct);
                return true;
            });

        await deleteAttempt.Should().ThrowAsync<PostgresException>(
            "the immutability trigger rejects audit deletes");

        var stillThere = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            ct => new ValueTask<int>(
                host.Provider.DbContext.AuditEvents.AsNoTracking()
                    .CountAsync(a => a.EntityId == entityId && a.Action == "reject", ct)));

        stillThere.Should().Be(1);
    }

    [Fact]
    public async Task TenantFactory_creates_the_tenant_via_create_tenant_with_settings_and_owner()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var ownerId = await InsertUserAsync("Tenant owner");

        await using var host = Host.Create(_fixture.AppConnectionString);
        var factory = new TenantFactory(host.Provider);

        var tenantId = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<Guid>(factory.CreateAsync($"Factory {suffix}", ownerId, ct)));

        tenantId.Should().NotBeEmpty();

        var state = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                var status = await db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Status)
                    .SingleAsync(ct);
                var settings = await db.TenantSettings.AsNoTracking()
                    .CountAsync(s => s.TenantId == tenantId, ct);
                var membership = await db.Memberships.AsNoTracking()
                    .SingleAsync(m => m.TenantId == tenantId, ct);
                return (status, settings, membership);
            });

        state.status.Should().Be("active");
        state.settings.Should().Be(1, "app.create_tenant seeds the default settings");
        state.membership.UserId.Should().Be(ownerId);
        state.membership.Role.Should().Be(MemberRole.OWNER);
        state.membership.IsActive.Should().BeTrue();
    }

    private async Task<(Guid TenantId, Guid UserId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userId = await admin.InsertUserAsync($"auth-{suffix}@example.test", $"Auth {suffix}");
        var tenantId = await admin.CreateTenantAsync($"Auth Tenant {suffix}", userId);

        return (tenantId, userId);
    }

    private async Task<Guid> InsertUserAsync(string name)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        return await admin.InsertUserAsync($"u-{suffix}@example.test", name);
    }

    private static byte[] RandomHash()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
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
