using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var email = NormalizeEmail(input.Email);
        var userId = Guid.NewGuid();
        var passwordHash = _hasher.Hash(input.Password);
        var tenantName = string.IsNullOrWhiteSpace(input.TenantName) ? DefaultTenantName : input.TenantName.Trim();

        return await _runner.RunAsUserAsync(userId, async ct =>
        {
            var existing = await _users.FindByEmailAsync(email, ct);
            if (existing is not null) throw new ConflictException("Email already registered");

            await _users.CreateAsync(new NewUser(userId, email, input.Name.Trim(), passwordHash, AuthConstants.DefaultLocale, false), ct);
            var tenantId = await _tenantFactory.CreateAsync(tenantName, userId, ct);

            var session = await CreateSessionAsync(userId, tenantId, meta, ct);
            var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
            {
                UserId = userId, SessionId = session.SessionId, TenantId = tenantId, Role = nameof(MemberRole.OWNER), MustChangePassword = false,
            });

            IReadOnlyList<MembershipSummary> memberships = [new MembershipSummary(tenantId, tenantName, MemberRole.OWNER, false)];
            return new AuthResult(userId, memberships, accessToken, session.RefreshToken, session.RefreshExpiresAt);
        }, cancellationToken);
    }

    public async Task<SelectTenantResult> SelectTenantAsync(Guid userId, Guid tenantId, string? refreshToken, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);
        var user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedException(InvalidSession);

        var membership = await _memberships.FindAsync(tenantId, userId, cancellationToken);
        if (membership is null || !membership.IsActive) throw new ForbiddenException("The user is not an active member of the selected business");

        await _runner.ActivateAsync(TenantContext.ForTenant(tenantId, userId, membership.Role.ToDbRole()), cancellationToken);

        Guid? sessionId = null;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = _tokens.HashRefreshToken(refreshToken);
            var session = await _sessions.FindActiveByTokenHashAsync(hash, userId, cancellationToken);
            sessionId = session?.Id;
            if (session is not null) await _sessions.BindTenantAsync(hash, userId, tenantId, _clock.UtcNow, cancellationToken);
        }

        await _audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(userId).Entity("session").Action("select_tenant").After(new { tenantId }).Ip(meta.Ip).Build(), cancellationToken);

        var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
        {
            UserId = userId, SessionId = sessionId, TenantId = tenantId, Role = membership.Role.ToString(), MustChangePassword = user.MustChangePassword,
        });

        IReadOnlyList<MembershipSummary> memberships = [new MembershipSummary(tenantId, membership.TenantName, membership.Role, user.MustChangePassword)];
        return new SelectTenantResult(accessToken, memberships);
    }
}
