using System.Security.Cryptography;
using System.Text;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Auth;

/// <summary>
/// Authentication and account-security use cases (AGENTS.md sections 9.9 and 10).
/// Anonymous flows (register, login, refresh, recovery) run outside the HTTP
/// tenant scope and therefore open their own transaction; authenticated flows
/// reuse the request transaction opened by the middleware.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const string DefaultTenantName = "Mi negocio";
    private const string InvalidCredentials = "Invalid credentials";
    private const string InvalidSession = "Invalid session";

    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IMembershipRepository _memberships;
    private readonly ITenantFactory _tenantFactory;
    private readonly IAuditWriter _audit;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly ILoginAttemptStore _lockout;
    private readonly IPasswordResetRateLimiter _resetLimiter;
    private readonly ISessionRevocationStore _revocation;
    private readonly IMailSender _mail;
    private readonly AppConfig _config;
    private readonly IClock _clock;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ITenantTransactionRunner _runner;

    public AuthService(
        IUserRepository users,
        ISessionRepository sessions,
        IPasswordResetTokenRepository resetTokens,
        IMembershipRepository memberships,
        ITenantFactory tenantFactory,
        IAuditWriter audit,
        ITokenService tokens,
        IPasswordHasher hasher,
        ILoginAttemptStore lockout,
        IPasswordResetRateLimiter resetLimiter,
        ISessionRevocationStore revocation,
        IMailSender mail,
        AppConfig config,
        IClock clock,
        ITenantContextAccessor tenantContext,
        ITenantTransactionRunner runner)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(resetTokens);
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(tenantFactory);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(lockout);
        ArgumentNullException.ThrowIfNull(resetLimiter);
        ArgumentNullException.ThrowIfNull(revocation);
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(runner);

        _users = users;
        _sessions = sessions;
        _resetTokens = resetTokens;
        _memberships = memberships;
        _tenantFactory = tenantFactory;
        _audit = audit;
        _tokens = tokens;
        _hasher = hasher;
        _lockout = lockout;
        _resetLimiter = resetLimiter;
        _revocation = revocation;
        _mail = mail;
        _config = config;
        _clock = clock;
        _tenantContext = tenantContext;
        _runner = runner;
    }

    public async Task<AuthResult> RegisterAsync(
        RegisterInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var email = NormalizeEmail(input.Email);
        var userId = Guid.NewGuid();
        var passwordHash = _hasher.Hash(input.Password);
        var tenantName = string.IsNullOrWhiteSpace(input.TenantName) ? DefaultTenantName : input.TenantName.Trim();

        return await _runner.RunAsUserAsync(
            userId,
            async ct =>
            {
                var existing = await _users.FindByEmailAsync(email, ct);
                if (existing is not null)
                {
                    throw new ConflictException("Email already registered");
                }

                await _users.CreateAsync(
                    new NewUser(userId, email, input.Name.Trim(), passwordHash, AuthConstants.DefaultLocale, false),
                    ct);

                // app.create_tenant writes the tenant, its settings, the owner
                // membership, and the tenant-created audit row in one place.
                var tenantId = await _tenantFactory.CreateAsync(tenantName, userId, ct);

                var session = await CreateSessionAsync(userId, tenantId, meta, ct);
                var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
                {
                    UserId = userId,
                    SessionId = session.SessionId,
                    TenantId = tenantId,
                    Role = nameof(MemberRole.OWNER),
                    MustChangePassword = false,
                });

                IReadOnlyList<MembershipSummary> memberships =
                    [new MembershipSummary(tenantId, tenantName, MemberRole.OWNER, false)];

                return new AuthResult(
                    userId,
                    memberships,
                    accessToken,
                    session.RefreshToken,
                    session.RefreshExpiresAt);
            },
            cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(
        LoginInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var email = NormalizeEmail(input.Email);

        if (await _lockout.IsBlockedAsync(email, cancellationToken))
        {
            throw new TooManyRequestsException("Too many failed attempts. Try again later.");
        }

        var user = await InNoneTxAsync(
            async ct => await _users.FindByEmailAsync(email, ct),
            cancellationToken);

        if (user is null || !user.IsActive || !_hasher.Verify(user.PasswordHash, input.Password))
        {
            await _lockout.RegisterFailureAsync(email, cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        await _lockout.ResetAsync(email, cancellationToken);

        return await _runner.RunAsUserAsync(
            user.Id,
            async ct =>
            {
                var active = await _memberships.ListActiveByUserAsync(user.Id, ct);
                if (active.Count == 0)
                {
                    throw new ForbiddenException("The account has no active business membership");
                }

                await _users.UpdateLastLoginAsync(user.Id, _clock.UtcNow, ct);

                var selected = active.Count == 1 ? active[0] : null;
                var session = await CreateSessionAsync(user.Id, selected?.TenantId, meta, ct);
                var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
                {
                    UserId = user.Id,
                    SessionId = session.SessionId,
                    TenantId = selected?.TenantId,
                    Role = selected?.Role.ToString(),
                    MustChangePassword = user.MustChangePassword,
                });

                return new AuthResult(
                    user.Id,
                    ToSummaries(active, user.MustChangePassword),
                    accessToken,
                    session.RefreshToken,
                    session.RefreshExpiresAt);
            },
            cancellationToken);
    }

    public async Task<SelectTenantResult> SelectTenantAsync(
        Guid userId,
        Guid tenantId,
        string? refreshToken,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);

        var user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException(InvalidSession);
        }

        var membership = await _memberships.FindAsync(tenantId, userId, cancellationToken);
        if (membership is null || !membership.IsActive)
        {
            throw new ForbiddenException("The user is not an active member of the selected business");
        }

        // Switch the current request transaction to the selected tenant so RLS
        // and audit writes see the right identity from here on.
        await _runner.ActivateAsync(
            TenantContext.ForTenant(tenantId, userId, membership.Role.ToDbRole()),
            cancellationToken);

        Guid? sessionId = null;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = _tokens.HashRefreshToken(refreshToken);
            var session = await _sessions.FindActiveByTokenHashAsync(hash, userId, cancellationToken);
            sessionId = session?.Id;

            if (session is not null)
            {
                await _sessions.BindTenantAsync(hash, userId, tenantId, _clock.UtcNow, cancellationToken);
            }
        }

        await _audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(userId)
                .Entity("session")
                .Action("select_tenant")
                .After(new { tenantId })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken);

        var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            TenantId = tenantId,
            Role = membership.Role.ToString(),
            MustChangePassword = user.MustChangePassword,
        });

        IReadOnlyList<MembershipSummary> memberships =
            [new MembershipSummary(tenantId, membership.TenantName, membership.Role, user.MustChangePassword)];

        return new SelectTenantResult(accessToken, memberships);
    }

    public async Task<TokenPair> RefreshAsync(
        string rawRefreshToken,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);

        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            throw new UnauthorizedException(InvalidSession);
        }

        var hash = _tokens.HashRefreshToken(rawRefreshToken);
        var session = await InNoneTxAsync(
            async ct => await _sessions.FindByTokenHashAsync(hash, ct),
            cancellationToken);

        if (session is null)
        {
            throw new UnauthorizedException(InvalidSession);
        }

        if (session.RevokedAt is not null)
        {
            // Reuse of a rotated token: burn the whole family.
            await BurnFamilyAsync(session.UserId, session.TenantId, meta, cancellationToken);
            throw new UnauthorizedException("Refresh token reuse detected");
        }

        if (session.ExpiresAt <= _clock.UtcNow)
        {
            throw new UnauthorizedException(InvalidSession);
        }

        var context = new TenantContext(session.TenantId, session.UserId, null);

        return await InContextTxAsync(
            context,
            async ct =>
            {
                var user = await _users.FindByIdAsync(session.UserId, ct);
                if (user is null || !user.IsActive)
                {
                    throw new UnauthorizedException(InvalidSession);
                }

                MemberRole? role = null;
                if (session.TenantId is { } tenantId)
                {
                    var membership = await _memberships.FindAsync(tenantId, session.UserId, ct);
                    if (membership is null || !membership.IsActive)
                    {
                        throw new UnauthorizedException(InvalidSession);
                    }

                    role = membership.Role;
                }

                await _sessions.RevokeOwnedAsync(session.Id, session.UserId, _clock.UtcNow, ct);
                await _revocation.RevokeAsync(session.Id, ct);

                var rotated = await CreateSessionAsync(session.UserId, session.TenantId, meta, ct);
                var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
                {
                    UserId = session.UserId,
                    SessionId = rotated.SessionId,
                    TenantId = session.TenantId,
                    Role = role?.ToString(),
                    MustChangePassword = user.MustChangePassword,
                });

                return new TokenPair(accessToken, rotated.RefreshToken, rotated.RefreshExpiresAt);
            },
            cancellationToken);
    }

    public async Task LogoutAsync(
        Guid userId,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> revoked;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = _tokens.HashRefreshToken(refreshToken);
            var session = await _sessions.FindActiveByTokenHashAsync(hash, userId, cancellationToken);
            if (session is null)
            {
                revoked = [];
            }
            else
            {
                await _sessions.RevokeOwnedAsync(session.Id, userId, _clock.UtcNow, cancellationToken);
                revoked = [session.Id];
            }
        }
        else
        {
            revoked = await _sessions.RevokeAllForUserAsync(userId, _clock.UtcNow, cancellationToken);
        }

        foreach (var sessionId in revoked)
        {
            await _revocation.RevokeAsync(sessionId, cancellationToken);
        }

        if (_tenantContext.Current?.TenantId is { } tenantId)
        {
            await _audit.WriteAsync(
                new AuditEventBuilder()
                    .TenantId(tenantId)
                    .UserId(userId)
                    .Entity("session")
                    .Action("logout")
                    .After(new { revokedCount = revoked.Count })
                    .Build(),
                cancellationToken);
        }
    }

    public async Task<MeResult> MeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException("User not found");

        var memberships = await _memberships.ListActiveByUserAsync(userId, cancellationToken);

        var profile = new ProfileView(
            user.Id,
            user.Email,
            user.Name,
            user.PreferredLocale,
            user.MustChangePassword);

        return new MeResult(profile, ToSummaries(memberships, user.MustChangePassword));
    }

    public async Task<string> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException("User not found");

        if (!_hasher.Verify(user.PasswordHash, currentPassword))
        {
            throw new UnauthorizedException("The current password is incorrect");
        }

        await _users.UpdatePasswordAsync(userId, _hasher.Hash(newPassword), false, cancellationToken);

        var memberships = await _memberships.ListActiveByUserAsync(userId, cancellationToken);
        var selected = memberships.Count > 0 ? memberships[0] : null;

        var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            TenantId = selected?.TenantId ?? _tenantContext.Current?.TenantId,
            Role = selected?.Role.ToString(),
            MustChangePassword = false,
        });

        if (selected?.TenantId is { } tenantId)
        {
            await _audit.WriteAsync(
                new AuditEventBuilder()
                    .TenantId(tenantId)
                    .UserId(userId)
                    .Entity("user")
                    .EntityId(userId)
                    .Action("change_password")
                    .Build(),
                cancellationToken);
        }

        return accessToken;
    }

    public Task UpdateLocaleAsync(
        Guid userId,
        string preferredLocale,
        CancellationToken cancellationToken = default)
        => _users.UpdateLocaleAsync(userId, preferredLocale, cancellationToken);

    public async Task ForgotPasswordAsync(
        string email,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);

        var normalized = NormalizeEmail(email);

        if (meta.Ip is { } ip && !_resetLimiter.TryConsumeIp(ip.ToString()))
        {
            throw new TooManyRequestsException();
        }

        if (!_resetLimiter.TryConsumeEmail(normalized))
        {
            throw new TooManyRequestsException();
        }

        var user = await InNoneTxAsync(
            async ct => await _users.FindByEmailAsync(normalized, ct),
            cancellationToken);

        // Always the same neutral response: never reveal whether the account exists.
        if (user is null || !user.IsActive || _resetLimiter.IsWithinCooldown(normalized))
        {
            return;
        }

        await IssuePasswordResetAsync(
            user.Id,
            user.Email,
            new PasswordResetOptions(AuthConstants.ResetTokenLifetime, meta.Ip, null),
            cancellationToken);

        _resetLimiter.MarkIssued(normalized);
    }

    public async Task ResetPasswordAsync(
        string rawToken,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            throw new UnauthorizedException("Invalid or expired reset token");
        }

        var tokenHash = Sha256(rawToken);
        var token = await InNoneTxAsync(
            async ct => await _resetTokens.FindByTokenHashAsync(tokenHash, ct),
            cancellationToken);

        if (token is null || token.UsedAt is not null || token.ExpiresAt <= _clock.UtcNow)
        {
            throw new UnauthorizedException("Invalid or expired reset token");
        }

        var passwordHash = _hasher.Hash(newPassword);

        var revoked = await _runner.RunAsUserAsync(
            token.UserId,
            async ct =>
            {
                await _users.UpdatePasswordAsync(token.UserId, passwordHash, false, ct);
                await _resetTokens.MarkUsedAsync(token.Id, _clock.UtcNow, ct);
                return await _sessions.RevokeAllForUserAsync(token.UserId, _clock.UtcNow, ct);
            },
            cancellationToken);

        foreach (var sessionId in revoked)
        {
            await _revocation.RevokeAsync(sessionId, cancellationToken);
        }

        var tenantIds = await _runner.RunAsUserAsync(
            token.UserId,
            async ct =>
            {
                var active = await _memberships.ListActiveByUserAsync(token.UserId, ct);
                return (IReadOnlyList<Guid>)active.Select(m => m.TenantId).ToList();
            },
            cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            await _runner.RunAsSystemAsync(
                tenantId,
                async ct =>
                {
                    await _audit.WriteAsync(
                        new AuditEventBuilder()
                            .TenantId(tenantId)
                            .UserId(token.UserId)
                            .Actor(AuditActor.SYSTEM)
                            .Entity("user")
                            .EntityId(token.UserId)
                            .Action("reset_password")
                            .Build(),
                        ct);
                    return true;
                },
                cancellationToken);
        }

        var email = await InNoneTxAsync(
            async ct => (await _users.FindByIdAsync(token.UserId, ct))?.Email,
            cancellationToken);

        if (email is not null)
        {
            await _mail.SendAsync(MailTemplates.PasswordChanged(email, _config.BrandName), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<AuthSessionView>> ListSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _sessions.ListActiveAsync(userId, _clock.UtcNow, cancellationToken);

        return sessions
            .Select(s => new AuthSessionView(
                s.Id,
                s.DeviceLabel,
                s.Ip,
                s.CreatedAt,
                s.LastSeenAt,
                s.Id == currentSessionId))
            .ToList();
    }

    public async Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var revoked = await _sessions.RevokeOwnedAsync(sessionId, userId, _clock.UtcNow, cancellationToken);
        if (!revoked)
        {
            throw new NotFoundException("Session not found");
        }

        await _revocation.RevokeAsync(sessionId, cancellationToken);

        if (_tenantContext.Current?.TenantId is { } tenantId)
        {
            await _audit.WriteAsync(
                new AuditEventBuilder()
                    .TenantId(tenantId)
                    .UserId(userId)
                    .Entity("session")
                    .EntityId(sessionId)
                    .Action("revoke_session")
                    .Build(),
                cancellationToken);
        }
    }

    public async Task RevokeAllOtherSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> revoked = currentSessionId is { } keep
            ? await _sessions.RevokeAllExceptAsync(userId, keep, _clock.UtcNow, cancellationToken)
            : await _sessions.RevokeAllForUserAsync(userId, _clock.UtcNow, cancellationToken);

        foreach (var sessionId in revoked)
        {
            await _revocation.RevokeAsync(sessionId, cancellationToken);
        }

        if (revoked.Count > 0 && _tenantContext.Current?.TenantId is { } tenantId)
        {
            await _audit.WriteAsync(
                new AuditEventBuilder()
                    .TenantId(tenantId)
                    .UserId(userId)
                    .Entity("session")
                    .Action("revoke_sessions")
                    .After(new { revokedCount = revoked.Count })
                    .Build(),
                cancellationToken);
        }
    }

    public async Task IssuePasswordResetAsync(
        Guid userId,
        string email,
        PasswordResetOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var token = NewOpaqueToken();
        var tokenHash = Sha256(token);
        var expiresAt = _clock.UtcNow + options.Lifetime;

        await InUserTxAsync(
            userId,
            async ct =>
            {
                await _resetTokens.InvalidateUnusedForUserAsync(userId, _clock.UtcNow, ct);
                await _resetTokens.CreateAsync(
                    new NewPasswordResetToken(userId, tokenHash, expiresAt, options.RequestedIp, options.IssuedBySupportId),
                    ct);
                return true;
            },
            cancellationToken);

        var resetUrl = $"{_config.AppOrigin}/reset-password?token={token}";
        await _mail.SendAsync(MailTemplates.PasswordReset(email, resetUrl, _config.BrandName), cancellationToken);
    }

    private async Task BurnFamilyAsync(
        Guid userId,
        Guid? tenantId,
        RequestMeta meta,
        CancellationToken cancellationToken)
    {
        var revoked = await _runner.RunAsUserAsync(
            userId,
            ct => new ValueTask<IReadOnlyList<Guid>>(
                _sessions.RevokeAllForUserAsync(userId, _clock.UtcNow, ct)),
            cancellationToken);

        foreach (var sessionId in revoked)
        {
            await _revocation.RevokeAsync(sessionId, cancellationToken);
        }

        if (tenantId is { } tid)
        {
            await _runner.RunAsSystemAsync(
                tid,
                async ct =>
                {
                    await _audit.WriteAsync(
                        new AuditEventBuilder()
                            .TenantId(tid)
                            .UserId(userId)
                            .Actor(AuditActor.SYSTEM)
                            .Entity("session")
                            .Action("revoke_sessions")
                            .Reason("refresh_token_reuse")
                            .Ip(meta.Ip)
                            .Build(),
                        ct);
                    return true;
                },
                cancellationToken);
        }
    }

    private async Task<CreatedSession> CreateSessionAsync(
        Guid userId,
        Guid? tenantId,
        RequestMeta meta,
        CancellationToken cancellationToken)
    {
        var issued = _tokens.IssueRefreshToken();
        var expiresAt = _clock.UtcNow + TokenConstants.RefreshTokenLifetime;

        var sessionId = await _sessions.CreateAsync(
            new NewSession(
                userId,
                tenantId,
                issued.Hash,
                expiresAt,
                meta.UserAgent,
                meta.DeviceLabel,
                meta.Ip,
                _clock.UtcNow),
            cancellationToken);

        return new CreatedSession(sessionId, issued.Token, expiresAt);
    }

    private static List<MembershipSummary> ToSummaries(
        IReadOnlyList<MembershipRecord> memberships,
        bool mustChangePassword) =>
        memberships
            .Select(m => new MembershipSummary(m.TenantId, m.TenantName, m.Role, mustChangePassword))
            .ToList();

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string NewOpaqueToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Sha256(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Runs <paramref name="work"/> inside a user-scoped transaction, or reuses
    /// the ambient request transaction when one is already open (authenticated
    /// flows), avoiding a nested transaction.
    /// </summary>
    private Task<TResult> InUserTxAsync<TResult>(
        Guid userId,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken) =>
        _tenantContext.Current is null
            ? _runner.RunAsUserAsync(userId, work, cancellationToken)
            : work(cancellationToken).AsTask();

    /// <summary>
    /// Runs <paramref name="work"/> with no identity, or reuses the ambient
    /// transaction when one is already open.
    /// </summary>
    private Task<TResult> InNoneTxAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken) =>
        _tenantContext.Current is null
            ? _runner.RunAsync(TenantContext.None, work, cancellationToken)
            : work(cancellationToken).AsTask();

    /// <summary>
    /// Runs <paramref name="work"/> in the given context, or reuses the ambient
    /// transaction when one is already open.
    /// </summary>
    private Task<TResult> InContextTxAsync<TResult>(
        TenantContext context,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken) =>
        _tenantContext.Current is null
            ? _runner.RunAsync(context, work, cancellationToken)
            : work(cancellationToken).AsTask();

    private sealed record CreatedSession(Guid SessionId, string RefreshToken, DateTimeOffset RefreshExpiresAt);
}
