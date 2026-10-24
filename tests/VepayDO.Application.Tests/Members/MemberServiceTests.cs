using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Members;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Members;

/// <summary>
/// Unit tests for <see cref="MemberService"/> (UC-SET-03, UC-AUTH-09). They pin
/// the one-time temporary password, the last-active-owner guard, the immediate
/// session revocation on deactivation, and the one-audit-event invariant.
/// </summary>
public sealed class MemberServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_returns_the_tenant_members()
    {
        var harness = new Harness();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");

        var members = await harness.Service.ListAsync(CancellationToken.None);

        members.Should().ContainSingle();
        members[0].UserId.Should().Be(OwnerId);
        members[0].Role.Should().Be(MemberRole.OWNER);
    }

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Create_returns_a_temporary_password_once_and_forces_change()
    {
        var harness = new Harness();

        var result = await harness.Service.CreateAsync(
            new CreateMemberInput("  New.Verifier@Example.TEST ", "Nuevo Verificador", MemberRole.VERIFIER),
            Meta(),
            CancellationToken.None);

        result.TemporaryPassword.Should().NotBeNullOrEmpty();
        result.TemporaryPassword.Length.Should().BeGreaterThanOrEqualTo(12);
        result.Member.Email.Should().Be("new.verifier@example.test");
        result.Member.Role.Should().Be(MemberRole.VERIFIER);

        harness.Users.Created.Should().ContainSingle();
        var created = harness.Users.Created[0];
        created.MustChangePassword.Should().BeTrue();
        created.PasswordHash.Should().Be(harness.Hasher.Hash(result.TemporaryPassword));
    }

    [Fact]
    public async Task Create_uses_a_distinct_password_each_time()
    {
        var harness = new Harness();

        var first = await harness.Service.CreateAsync(
            new CreateMemberInput("a@example.test", "A", MemberRole.VERIFIER), Meta(), CancellationToken.None);
        var second = await harness.Service.CreateAsync(
            new CreateMemberInput("b@example.test", "B", MemberRole.VERIFIER), Meta(), CancellationToken.None);

        first.TemporaryPassword.Should().NotBe(second.TemporaryPassword);
    }

    [Fact]
    public async Task Create_rejects_an_email_that_already_exists()
    {
        var harness = new Harness();
        harness.Users.Seed("taken@example.test");

        var act = async () => await harness.Service.CreateAsync(
            new CreateMemberInput("taken@example.test", "Taken", MemberRole.VERIFIER),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_writes_exactly_one_audit_event()
    {
        var harness = new Harness();

        await harness.Service.CreateAsync(
            new CreateMemberInput("v@example.test", "Verifier", MemberRole.VERIFIER),
            Meta(),
            CancellationToken.None);

        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Entity.Should().Be("membership");
        harness.Audit.Events[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task Update_deactivates_a_verifier_and_revokes_their_sessions()
    {
        var harness = new Harness();
        var verifierId = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");
        harness.Memberships.Seed(TenantId, verifierId, "Verifier", MemberRole.VERIFIER, "v@example.test");
        harness.Sessions.Seed(verifierId, Guid.NewGuid());

        var member = await harness.Service.UpdateAsync(
            verifierId,
            new UpdateMemberInput(Role: null, IsActive: false),
            Meta(),
            CancellationToken.None);

        member.IsActive.Should().BeFalse();
        harness.Memberships.ActiveOf(verifierId).Should().BeFalse();
        harness.Sessions.RevokedFor(verifierId).Should().BeTrue();
        harness.Revocation.Revoked.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Update_cannot_deactivate_the_last_active_owner()
    {
        var harness = new Harness();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");

        var act = async () => await harness.Service.UpdateAsync(
            OwnerId,
            new UpdateMemberInput(Role: null, IsActive: false),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Update_cannot_demote_the_last_active_owner()
    {
        var harness = new Harness();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");

        var act = async () => await harness.Service.UpdateAsync(
            OwnerId,
            new UpdateMemberInput(Role: MemberRole.VERIFIER, IsActive: true),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Update_can_demote_an_owner_when_another_active_owner_exists()
    {
        var harness = new Harness();
        var secondOwner = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");
        harness.Memberships.Seed(TenantId, secondOwner, "Owner 2", MemberRole.OWNER, "owner2@example.test");

        var member = await harness.Service.UpdateAsync(
            OwnerId,
            new UpdateMemberInput(Role: MemberRole.VERIFIER, IsActive: true),
            Meta(),
            CancellationToken.None);

        member.Role.Should().Be(MemberRole.VERIFIER);
    }

    [Fact]
    public async Task Update_promotes_a_verifier_to_owner()
    {
        var harness = new Harness();
        var verifierId = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");
        harness.Memberships.Seed(TenantId, verifierId, "Verifier", MemberRole.VERIFIER, "v@example.test");

        var member = await harness.Service.UpdateAsync(
            verifierId,
            new UpdateMemberInput(Role: MemberRole.OWNER, IsActive: true),
            Meta(),
            CancellationToken.None);

        member.Role.Should().Be(MemberRole.OWNER);
        harness.Audit.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_of_an_unknown_member_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMemberInput(Role: MemberRole.VERIFIER, IsActive: true),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RevokeSessions_revokes_every_session_of_the_member()
    {
        var harness = new Harness();
        var verifierId = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, verifierId, "Verifier", MemberRole.VERIFIER, "v@example.test");
        harness.Sessions.Seed(verifierId, Guid.NewGuid());
        harness.Sessions.Seed(verifierId, Guid.NewGuid());

        await harness.Service.RevokeSessionsAsync(verifierId, Meta(), CancellationToken.None);

        harness.Sessions.RevokedFor(verifierId).Should().BeTrue();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("revoke_member_sessions");
    }

    [Fact]
    public async Task RevokeSessions_of_an_unknown_member_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.RevokeSessionsAsync(Guid.NewGuid(), Meta(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static RequestMeta Meta() => new(null, null, null);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Users = new FakeUserRepository();
            Memberships = new FakeMembershipRepository();
            Sessions = new FakeSessionRepository();
            Revocation = new FakeSessionRevocationStore();
            Hasher = new FakePasswordHasher();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, OwnerId, DbRole.Owner);
            Service = new MemberService(
                Users,
                Memberships,
                Sessions,
                Revocation,
                Hasher,
                Audit,
                new FixedClock(Now),
                new FakeTenantContextAccessor(ctx));
        }

        public FakeUserRepository Users { get; }

        public FakeMembershipRepository Memberships { get; }

        public FakeSessionRepository Sessions { get; }

        public FakeSessionRevocationStore Revocation { get; }

        public FakePasswordHasher Hasher { get; }

        public RecordingAuditWriter Audit { get; }

        public MemberService Service { get; }
    }

    private sealed class FakeTenantContextAccessor(TenantContext? context) : ITenantContextAccessor
    {
        public TenantContext? Current { get; } = context;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, UserRecord> _byEmail = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, UserRecord> _byId = [];

        public List<NewUser> Created { get; } = [];

        public void Seed(string email)
        {
            var id = Guid.NewGuid();
            var record = new UserRecord
            {
                Id = id,
                Email = email,
                Name = email,
                PasswordHash = "hash:x",
                IsActive = true,
                PreferredLocale = "es-DO",
                MustChangePassword = false,
                IsPlatformSupport = false,
            };

            _byEmail[email] = record;
            _byId[id] = record;
        }

        public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byEmail.GetValueOrDefault(email));

        public Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.GetValueOrDefault(userId));

        public Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
        {
            Created.Add(user);
            var record = new UserRecord
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                PasswordHash = user.PasswordHash,
                IsActive = true,
                PreferredLocale = user.PreferredLocale,
                MustChangePassword = user.MustChangePassword,
                IsPlatformSupport = false,
            };

            _byEmail[user.Email] = record;
            _byId[user.Id] = record;
            return Task.CompletedTask;
        }

        public Task UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetPlatformSupportAsync(Guid userId, bool isPlatformSupport, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeMembershipRepository : IMembershipRepository
    {
        private readonly Dictionary<Guid, MembershipRecord> _byUser = [];

        public void Seed(Guid tenantId, Guid userId, string name, MemberRole role, string email)
        {
            _byUser[userId] = new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = "Tenant",
                UserName = name,
                UserEmail = email,
                CreatedAt = Now,
            };
        }

        public bool ActiveOf(Guid userId) => _byUser[userId].IsActive;

        public Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _byUser.Values.Where(m => m.UserId == userId && m.IsActive).ToList();
            return Task.FromResult(items);
        }

        public Task<MembershipRecord?> FindAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byUser.GetValueOrDefault(userId));

        public Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _byUser.Values.Where(m => m.TenantId == tenantId).ToList();
            return Task.FromResult(items);
        }

        public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byUser.Values.Count(m => m.TenantId == tenantId && m.IsActive && m.Role == MemberRole.OWNER));

        public Task AddAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
        {
            _byUser[userId] = new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = "Tenant",
                UserName = userId.ToString(),
                UserEmail = userId.ToString(),
                CreatedAt = Now,
            };

            return Task.CompletedTask;
        }

        public Task UpdateRoleAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
        {
            _byUser[userId] = _byUser[userId] with { Role = role };
            return Task.CompletedTask;
        }

        public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken = default)
        {
            _byUser[userId] = _byUser[userId] with { IsActive = isActive };
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        private readonly List<SessionRecord> _sessions = [];

        public void Seed(Guid userId, Guid sessionId)
        {
            _sessions.Add(new SessionRecord
            {
                Id = sessionId,
                UserId = userId,
                ExpiresAt = Now.AddDays(30),
                CreatedAt = Now,
            });
        }

        public bool RevokedFor(Guid userId) =>
            _sessions.Where(s => s.UserId == userId).All(s => s.RevokedAt is not null);

        public Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SessionRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SessionRecord?> FindActiveByTokenHashAsync(byte[] tokenHash, Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SessionRecord>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RevokeOwnedAsync(Guid sessionId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var ids = new List<Guid>();
            for (var index = 0; index < _sessions.Count; index++)
            {
                var session = _sessions[index];
                if (session.UserId == userId && session.RevokedAt is null)
                {
                    _sessions[index] = session with { RevokedAt = at };
                    ids.Add(session.Id);
                }
            }

            return Task.FromResult<IReadOnlyList<Guid>>(ids);
        }

        public Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(Guid userId, Guid keepSessionId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task BindTenantAsync(byte[] tokenHash, Guid userId, Guid tenantId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSessionRevocationStore : ISessionRevocationStore
    {
        public List<Guid> Revoked { get; } = [];

        public Task<bool> IsRevokedAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Revoked.Contains(sessionId));

        public Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            Revoked.Add(sessionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plainPassword) => $"hash:{plainPassword}";

        public bool Verify(string passwordHash, string plainPassword) =>
            string.Equals(passwordHash, $"hash:{plainPassword}", StringComparison.Ordinal);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEventDraft> Events { get; } = [];

        public Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default)
        {
            Events.Add(draft);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}