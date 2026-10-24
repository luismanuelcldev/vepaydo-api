using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService
{
    public async Task<TokenPair> RefreshAsync(string rawRefreshToken, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);
        if (string.IsNullOrEmpty(rawRefreshToken)) throw new UnauthorizedException(InvalidSession);

        var hash = _tokens.HashRefreshToken(rawRefreshToken);
        var session = await InNoneTxAsync(async ct => await _sessions.FindByTokenHashAsync(hash, ct), cancellationToken);

        if (session is null) throw new UnauthorizedException(InvalidSession);
        if (session.RevokedAt is not null)
        {
            await BurnFamilyAsync(session.UserId, session.TenantId, meta, cancellationToken);
            throw new UnauthorizedException("Refresh token reuse detected");
        }
        if (session.ExpiresAt <= _clock.UtcNow) throw new UnauthorizedException(InvalidSession);

        var context = new TenantContext(session.TenantId, session.UserId, null);
        return await InContextTxAsync(context, async ct =>
        {
            var user = await _users.FindByIdAsync(session.UserId, ct);
            if (user is null || !user.IsActive) throw new UnauthorizedException(InvalidSession);

            MemberRole? role = null;
            if (session.TenantId is { } tenantId)
            {
                var membership = await _memberships.FindAsync(tenantId, session.UserId, ct);
                if (membership is null || !membership.IsActive) throw new UnauthorizedException(InvalidSession);
                role = membership.Role;
            }

            await _sessions.RevokeOwnedAsync(session.Id, session.UserId, _clock.UtcNow, ct);
            await _revocation.RevokeAsync(session.Id, ct);

            var rotated = await CreateSessionAsync(session.UserId, session.TenantId, meta, ct);
            var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
            {
                UserId = session.UserId, SessionId = rotated.SessionId, TenantId = session.TenantId, Role = role?.ToString(), MustChangePassword = user.MustChangePassword,
            });

            return new TokenPair(accessToken, rotated.RefreshToken, rotated.RefreshExpiresAt);
        }, cancellationToken);
    }

    private async Task BurnFamilyAsync(Guid userId, Guid? tenantId, RequestMeta meta, CancellationToken cancellationToken)
    {
        var revoked = await _runner.RunAsUserAsync(userId, ct => new ValueTask<IReadOnlyList<Guid>>(_sessions.RevokeAllForUserAsync(userId, _clock.UtcNow, ct)), cancellationToken);
        foreach (var sessionId in revoked) await _revocation.RevokeAsync(sessionId, cancellationToken);

        if (tenantId is { } tid)
        {
            await _runner.RunAsSystemAsync(tid, async ct =>
            {
                await _audit.WriteAsync(new AuditEventBuilder().TenantId(tid).UserId(userId).Actor(AuditActor.SYSTEM).Entity("session").Action("revoke_sessions").Reason("refresh_token_reuse").Ip(meta.Ip).Build(), ct);
                return true;
            }, cancellationToken);
        }
    }
}
