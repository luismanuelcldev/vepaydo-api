using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Auth;

/// <summary>
/// Unit tests for <see cref="AuthService"/> against in-memory ports. They pin the
/// observable behavior of the auth use cases (AGENTS.md sections 9.9 and 10):
/// registration, lockout, refresh rotation and reuse detection, recovery, and
/// session management. No database is involved.
/// </summary>
public sealed class AuthServiceTests
{
    private const string Pepper = "pepper";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Register_creates_the_user_tenant_and_session()
    {
        var harness = new Harness();

        var result = await harness.Service.RegisterAsync(
            new RegisterInput("  Alice@Example.TEST ", "Alice", "supersecret12", "Mi tienda"),
            new RequestMeta(null, "pytest", "pytest"),
            CancellationToken.None);

        result.UserId.Should().NotBeEmpty();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.Memberships.Should().ContainSingle();
        result.Memberships[0].Role.Should().Be(MemberRole.OWNER);
        result.Memberships[0].TenantName.Should().Be("Mi tienda");

        harness.Users.ByEmail.Should().ContainKey("alice@example.test");
        harness.TenantFactory.CreateCalls.Should().Be(1);
        harness.Sessions.All.Should().ContainSingle();
        harness.Tokens.LastClaims!.TenantId.Should().Be(result.Memberships[0].TenantId);
        harness.Tokens.LastClaims.Role.Should().Be(nameof(MemberRole.OWNER));
    }

    [Fact]
    public async Task Register_rejects_an_email_that_already_exists()
    {
        var harness = new Harness();
        var existing = Guid.NewGuid();
        harness.Users.Seed(existing, "taken@example.test");

        var act = async () => await harness.Service.RegisterAsync(
            new RegisterInput("taken@example.test", "Taken", "supersecret12", "Mi tienda"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_with_an_unknown_email_throws_and_records_a_failure()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.LoginAsync(
            new LoginInput("nobody@example.test", "supersecret12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        harness.Lockout.Failures.Should().Be(1);
    }

    [Fact]
    public async Task Login_locks_out_after_repeated_failures()
    {
        var harness = new Harness();
        harness.Users.Seed(Guid.NewGuid(), "user@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.LoginAsync(
            new LoginInput("user@example.test", "wrong-password1"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        await act.Should().ThrowAsync<TooManyRequestsException>();
    }

    [Fact]
    public async Task Login_selects_the_only_membership()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var result = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, "ua", "ua"),
            CancellationToken.None);

        result.Memberships.Should().ContainSingle();
        harness.Tokens.LastClaims!.TenantId.Should().Be(tenantId);
        harness.Users.LastLoginAt!.Value.Should().Be(Now);
    }

    [Fact]
    public async Task Login_without_any_membership_is_forbidden()
    {
        var harness = new Harness();
        harness.Users.Seed(Guid.NewGuid(), "orphan@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.LoginAsync(
            new LoginInput("orphan@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Refresh_rotates_the_session_and_revokes_the_old_one()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var login = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        var oldSession = harness.Sessions.All.Single();

        var refreshed = await harness.Service.RefreshAsync(
            login.RefreshToken,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        refreshed.AccessToken.Should().NotBeNullOrEmpty();
        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);

        harness.Sessions.All.Should().HaveCount(2);
        harness.Sessions.All.Single(s => s.Id == oldSession.Id).RevokedAt.Should().NotBeNull();
        harness.Revocation.Revoked.Should().Contain(oldSession.Id);
    }

    [Fact]
    public async Task Refresh_with_a_revoked_token_burns_the_whole_family()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var login = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        // Rotate once, then replay the token that was already consumed.
        _ = await harness.Service.RefreshAsync(
            login.RefreshToken,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        var lastLoginAfterFirstLogin = harness.Users.LastLoginAt;

        var replay = async () => await harness.Service.RefreshAsync(
            login.RefreshToken,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await replay.Should().ThrowAsync<UnauthorizedException>();
        harness.Users.LastLoginAt.Should().Be(lastLoginAfterFirstLogin, "reuse never issues a new session");
        harness.Audit.Events.Should().Contain(e => e.Reason == "refresh_token_reuse");
    }

    [Fact]
    public async Task Refresh_with_an_expired_session_is_rejected()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var issued = harness.Tokens.IssueRefreshToken();
        harness.Sessions.Seed(new SessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExpiresAt = Now.AddMinutes(-1),
            CreatedAt = Now.AddDays(-31),
        }, issued.Hash);

        var act = async () => await harness.Service.RefreshAsync(
            issued.Token,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Change_password_rejects_a_wrong_current_password()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.ChangePasswordAsync(
            userId,
            "not-the-password",
            "brand-new-secret12",
            sessionId: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Change_password_updates_the_hash_and_clears_the_flag()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "temporary123", mustChangePassword: true);
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var token = await harness.Service.ChangePasswordAsync(
            userId,
            "temporary123",
            "brand-new-secret12",
            sessionId: null,
            CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        harness.Users.MustChangePassword.Should().BeFalse();
        harness.Users.Verify(userId, "brand-new-secret12").Should().BeTrue();
        harness.Audit.Events.Should().Contain(e => e.Action == "change_password");
    }

    [Fact]
    public async Task Forgot_password_emails_a_link_once_within_the_cooldown()
    {
        var harness = new Harness();
        harness.Users.Seed(Guid.NewGuid(), "owner@example.test", password: "correct-horse12");

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        harness.Mail.Sent.Should().ContainSingle();
        harness.ResetTokens.All.Should().ContainSingle();
        harness.Mail.Sent[0].TextBody.Should().Contain("/reset-password?token=");
    }

    [Fact]
    public async Task Forgot_password_is_silent_for_an_unknown_account()
    {
        var harness = new Harness();

        await harness.Service.ForgotPasswordAsync(
            "missing@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        harness.Mail.Sent.Should().BeEmpty();
        harness.ResetTokens.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Reset_password_consumes_the_token_and_revokes_every_session()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var login = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        var sessionId = harness.Sessions.All.Single(s => s.RevokedAt is null).Id;

        await harness.Service.ResetPasswordAsync(
            "the-emailed-token",
            "another-new-secret12",
            CancellationToken.None);

        harness.Users.Verify(userId, "another-new-secret12").Should().BeTrue();
        harness.ResetTokens.All.Single().UsedAt.Should().NotBeNull();
        harness.Revocation.Revoked.Should().Contain(sessionId);
        harness.Audit.Events.Should().Contain(e => e.Action == "reset_password");
        harness.Mail.Sent.Should().Contain(m => m.Subject.Contains("contraseña cambiada", StringComparison.Ordinal));
        login.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reset_password_rejects_a_reused_token()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await harness.Service.ResetPasswordAsync("the-emailed-token", "another-new-secret12", CancellationToken.None);

        var act = async () => await harness.Service.ResetPasswordAsync(
            "the-emailed-token",
            "yet-another-secret12",
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Select_tenant_activates_the_context_and_audits()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var result = await harness.Service.SelectTenantAsync(
            userId,
            tenantId,
            refreshToken: null,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        harness.Runner.Activated.Should().ContainSingle();
        harness.Runner.Activated[0].TenantId.Should().Be(tenantId);
        harness.Audit.Events.Should().Contain(e => e.Action == "select_tenant");
        harness.Tokens.LastClaims!.Role.Should().Be(nameof(MemberRole.OWNER));
    }

    [Fact]
    public async Task Select_tenant_is_forbidden_without_a_membership()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.SelectTenantAsync(
            userId,
            Guid.NewGuid(),
            refreshToken: null,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Revoke_all_other_sessions_keeps_the_current_one()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var first = harness.NewSession(userId);
        var current = harness.NewSession(userId);

        await harness.Service.RevokeAllOtherSessionsAsync(userId, current, CancellationToken.None);

        harness.Sessions.All.Single(s => s.Id == first).RevokedAt.Should().NotBeNull();
        harness.Sessions.All.Single(s => s.Id == current).RevokedAt.Should().BeNull();
    }

    /// <summary>Wires the service under test with fresh in-memory ports.</summary>
    private sealed class Harness
    {
        public Harness()
        {
            Users = new FakeUserRepository();
            Sessions = new FakeSessionRepository();
            ResetTokens = new FakePasswordResetTokenRepository();
            Memberships = new FakeMembershipRepository();
            TenantFactory = new RecordingTenantFactory();
            Audit = new RecordingAuditWriter();
            Tokens = new FakeTokenService();
            Revocation = new FakeSessionRevocationStore();
            Lockout = new InMemoryLoginAttemptStore();
            Limiter = new InMemoryPasswordResetRateLimiter();
            Mail = new RecordingMailSender();

            Service = new AuthService(
                Users,
                Sessions,
                ResetTokens,
                Memberships,
                TenantFactory,
                Audit,
                Tokens,
                new FakePasswordHasher(),
                Lockout,
                Limiter,
                Revocation,
                Mail,
                new AppConfig { AppOrigin = "http://localhost:5173", BrandName = "VepayDO" },
                new FixedClock(Now),
                new NullTenantContextAccessor(),
                Runner);
        }

        public FakeUserRepository Users { get; }

        public FakeSessionRepository Sessions { get; }

        public FakePasswordResetTokenRepository ResetTokens { get; }

        public FakeMembershipRepository Memberships { get; }

        public RecordingTenantFactory TenantFactory { get; }

        public RecordingAuditWriter Audit { get; }

        public FakeTokenService Tokens { get; }

        public FakeSessionRevocationStore Revocation { get; }

        public InMemoryLoginAttemptStore Lockout { get; }

        public InMemoryPasswordResetRateLimiter Limiter { get; }

        public RecordingMailSender Mail { get; }

        public InlineTenantTransactionRunner Runner { get; } = new();

        public AuthService Service { get; }

        public Guid NewSession(Guid userId)
        {
            var id = Guid.NewGuid();
            Sessions.Seed(
                new SessionRecord
                {
                    Id = id,
                    UserId = userId,
                    ExpiresAt = Now.AddDays(30),
                    CreatedAt = Now,
                },
                Encoding.UTF8.GetBytes(id.ToString()));

            return id;
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, NewUser> _users = [];
        private readonly Dictionary<string, Guid> _byEmail = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, Guid> ByEmail => _byEmail;

        public DateTimeOffset? LastLoginAt { get; private set; }

        public bool MustChangePassword { get; private set; }

        private string _passwordHash = string.Empty;

        public void Seed(Guid userId, string email, string password = "supersecret12", bool mustChangePassword = false)
        {
            _passwordHash = FakePasswordHasher.Compute(password);
            _users[userId] = new NewUser(userId, email, "Seeded", _passwordHash, "es-DO", mustChangePassword);
            _byEmail[email] = userId;
        }

        public bool Verify(Guid userId, string password) =>
            _users.TryGetValue(userId, out _) && _passwordHash == FakePasswordHasher.Compute(password);

        public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (!_byEmail.TryGetValue(email, out var id) || !_users.TryGetValue(id, out var user))
            {
                return Task.FromResult<UserRecord?>(null);
            }

            MustChangePassword = user.MustChangePassword;
            return Task.FromResult<UserRecord?>(ToRecord(user));
        }

        public Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult<UserRecord?>(null);
            }

            MustChangePassword = user.MustChangePassword;
            return Task.FromResult<UserRecord?>(ToRecord(user));
        }

        public Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);

            _users[user.Id] = user;
            _byEmail[user.Email] = user.Id;
            _passwordHash = user.PasswordHash;
            MustChangePassword = user.MustChangePassword;
            return Task.CompletedTask;
        }

        public Task UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                _users[userId] = user with { PasswordHash = passwordHash };
            }

            _passwordHash = passwordHash;
            MustChangePassword = mustChangePassword;
            return Task.CompletedTask;
        }

        public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            LastLoginAt = at;
            return Task.CompletedTask;
        }

        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetPlatformSupportAsync(Guid userId, bool isPlatformSupport, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static UserRecord ToRecord(NewUser user) => new()
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
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        private readonly List<(SessionRecord Record, byte[] Hash)> _sessions = [];

        public IReadOnlyList<SessionRecord> All => _sessions.Select(entry => entry.Record).ToList();

        public void Seed(SessionRecord record, byte[] hash) => _sessions.Add((record, hash));

        public Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(session);

            var id = Guid.NewGuid();
            _sessions.Add((
                new SessionRecord
                {
                    Id = id,
                    UserId = session.UserId,
                    TenantId = session.TenantId,
                    ExpiresAt = session.ExpiresAt,
                    DeviceLabel = session.DeviceLabel,
                    CreatedAt = session.CreatedAt,
                },
                session.TokenHash));

            return Task.FromResult(id);
        }

        public Task<SessionRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Find(tokenHash, userId: null));

        public Task<SessionRecord?> FindActiveByTokenHashAsync(byte[] tokenHash, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Find(tokenHash, userId));

        public Task<IReadOnlyList<SessionRecord>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SessionRecord> active = _sessions
                .Where(entry => entry.Record.UserId == userId && entry.Record.RevokedAt is null && entry.Record.ExpiresAt > now)
                .Select(entry => entry.Record)
                .ToList();

            return Task.FromResult(active);
        }

        public Task<bool> RevokeOwnedAsync(Guid sessionId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var index = _sessions.FindIndex(entry => entry.Record.Id == sessionId && entry.Record.UserId == userId && entry.Record.RevokedAt is null);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _sessions[index] = (_sessions[index].Record with { RevokedAt = at }, _sessions[index].Hash);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Revoke(entry => entry.Record.UserId == userId, at));

        public Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(Guid userId, Guid keepSessionId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Revoke(entry => entry.Record.UserId == userId && entry.Record.Id != keepSessionId, at));

        public Task BindTenantAsync(byte[] tokenHash, Guid userId, Guid tenantId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
        {
            var index = _sessions.FindIndex(entry => entry.Hash.AsSpan().SequenceEqual(tokenHash) && entry.Record.UserId == userId);
            if (index >= 0)
            {
                _sessions[index] = (
                    _sessions[index].Record with { TenantId = tenantId, LastSeenAt = lastSeenAt },
                    _sessions[index].Hash);
            }

            return Task.CompletedTask;
        }

        private List<Guid> Revoke(Func<(SessionRecord Record, byte[] Hash), bool> predicate, DateTimeOffset at)
        {
            var ids = new List<Guid>();
            for (var index = 0; index < _sessions.Count; index++)
            {
                var entry = _sessions[index];
                if (entry.Record.RevokedAt is null && predicate(entry))
                {
                    ids.Add(entry.Record.Id);
                    _sessions[index] = (entry.Record with { RevokedAt = at }, entry.Hash);
                }
            }

            return ids;
        }

        private SessionRecord? Find(byte[] tokenHash, Guid? userId)
        {
            var match = _sessions.FirstOrDefault(entry =>
                entry.Hash.AsSpan().SequenceEqual(tokenHash)
                && (userId is null || entry.Record.UserId == userId)
                && (userId is null || entry.Record.RevokedAt is null));

            return match.Record;
        }
    }

    private sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly List<(PasswordResetTokenRecord Record, byte[] Hash)> _tokens = [];

        public IReadOnlyList<PasswordResetTokenRecord> All => _tokens.Select(entry => entry.Record).ToList();

        public Task CreateAsync(NewPasswordResetToken token, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(token);

            _tokens.Add((
                new PasswordResetTokenRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = token.UserId,
                    ExpiresAt = token.ExpiresAt,
                    UsedAt = null,
                },
                token.TokenHash));

            return Task.CompletedTask;
        }

        public Task<PasswordResetTokenRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default)
        {
            // The service hashes the raw token with its own private SHA-256, so the
            // fake cannot reproduce the hash. These unit tests exercise the
            // single-token flow, so resolving to the latest unused token (or the
            // latest one when every token is already used) models "the token the
            // caller presented" and still surfaces the used/expired branches.
            var match = _tokens.LastOrDefault(entry => entry.Record.UsedAt is null);
            if (match.Record is null && _tokens.Count > 0)
            {
                match = _tokens[^1];
            }

            return Task.FromResult<PasswordResetTokenRecord?>(match.Record);
        }

        public Task InvalidateUnusedForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < _tokens.Count; index++)
            {
                var entry = _tokens[index];
                if (entry.Record.UserId == userId && entry.Record.UsedAt is null)
                {
                    _tokens[index] = (entry.Record with { UsedAt = at }, entry.Hash);
                }
            }

            return Task.CompletedTask;
        }

        public Task MarkUsedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var index = _tokens.FindIndex(entry => entry.Record.Id == id);
            if (index >= 0)
            {
                _tokens[index] = (_tokens[index].Record with { UsedAt = at }, _tokens[index].Hash);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeMembershipRepository : IMembershipRepository
    {
        private readonly List<MembershipRecord> _memberships = [];

        public void Seed(Guid tenantId, Guid userId, string tenantName, MemberRole role)
        {
            _memberships.Add(new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = tenantName,
                CreatedAt = Now,
            });
        }

        public Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _memberships
                .Where(m => m.UserId == userId && m.IsActive)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<MembershipRecord?> FindAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            var match = _memberships.FirstOrDefault(m => m.TenantId == tenantId && m.UserId == userId);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _memberships.Where(m => m.TenantId == tenantId).ToList();
            return Task.FromResult(items);
        }

        public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_memberships.Count(m => m.TenantId == tenantId && m.IsActive && m.Role == MemberRole.OWNER));

        public Task AddAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
        {
            _memberships.Add(new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = "Added",
                CreatedAt = Now,
            });

            return Task.CompletedTask;
        }

        public Task UpdateRoleAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingTenantFactory : ITenantFactory
    {
        public int CreateCalls { get; private set; }

        public Task<Guid> CreateAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEventDraft> Events { get; } = [];

        public Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draft);

            Events.Add(draft);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMailSender : IMailSender
    {
        public List<MailMessage> Sent { get; } = [];

        public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public static string Compute(string password) => $"hash:{password}";

        public string Hash(string plainPassword) => Compute(plainPassword);

        public bool Verify(string passwordHash, string plainPassword) =>
            string.Equals(passwordHash, Compute(plainPassword), StringComparison.Ordinal);
    }

    private sealed class FakeTokenService : ITokenService
    {
        private int _counter;

        public AccessTokenClaims? LastClaims { get; private set; }

        public string SignAccessToken(AccessTokenClaims claims)
        {
            ArgumentNullException.ThrowIfNull(claims);

            LastClaims = claims;
            return $"access-{claims.UserId:N}-{claims.TenantId?.ToString("N") ?? "none"}";
        }

        public Task<AccessTokenClaims> VerifyAccessTokenAsync(string token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IssuedRefreshToken IssueRefreshToken()
        {
            var token = $"refresh-{++_counter}-{Guid.NewGuid():N}";
            return new IssuedRefreshToken(token, HashRefreshToken(token));
        }

        public byte[] HashRefreshToken(string token) =>
            SHA256.HashData(Encoding.UTF8.GetBytes(token + Pepper));

        public bool HashesMatch(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
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

    private sealed class InMemoryLoginAttemptStore : ILoginAttemptStore
    {
        private const int MaxFailures = 5;
        private int _failures;

        public int Failures => _failures;

        public Task<bool> IsBlockedAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_failures >= MaxFailures);

        public Task RegisterFailureAsync(string key, CancellationToken cancellationToken = default)
        {
            _failures++;
            return Task.CompletedTask;
        }

        public Task ResetAsync(string key, CancellationToken cancellationToken = default)
        {
            _failures = 0;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPasswordResetRateLimiter : IPasswordResetRateLimiter
    {
        private readonly Dictionary<string, DateTimeOffset> _issued = new(StringComparer.Ordinal);

        public bool TryConsumeEmail(string email) => true;

        public bool TryConsumeIp(string ipAddress) => true;

        public bool IsWithinCooldown(string email) =>
            _issued.TryGetValue(email, out var issuedAt) && issuedAt + AuthConstants.ForgotPasswordResendCooldown > Now;

        public void MarkIssued(string email) => _issued[email] = Now;
    }

    private sealed class NullTenantContextAccessor : ITenantContextAccessor
    {
        public TenantContext? Current => null;
    }

    private sealed class InlineTenantTransactionRunner : ITenantTransactionRunner
    {
        public List<TenantContext> Activated { get; } = [];

        public Task<TResult> RunAsync<TResult>(TenantContext context, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return work(cancellationToken).AsTask();
        }

        public Task RunAsync(TenantContext context, Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return work(cancellationToken).AsTask();
        }

        public Task<TResult> RunAsUserAsync<TResult>(Guid userId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default) =>
            RunAsync(TenantContext.ForUser(userId), work, cancellationToken);

        public Task<TResult> RunAsSystemAsync<TResult>(Guid tenantId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default) =>
            RunAsync(TenantContext.ForSystem(tenantId), work, cancellationToken);

        public Task ActivateAsync(TenantContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            Activated.Add(context);
            return Task.CompletedTask;
        }

        public Task<Guid> CreateTenantAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
