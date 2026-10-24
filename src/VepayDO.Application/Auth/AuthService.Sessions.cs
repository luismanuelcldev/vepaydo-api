using VepayDO.Application.Audit;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService
{
    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> revoked;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = _tokens.HashRefreshToken(refreshToken);
            var session = await _sessions.FindActiveByTokenHashAsync(hash, userId, cancellationToken);
            if (session is null) revoked = [];
            else
            {
                await _sessions.RevokeOwnedAsync(session.Id, userId, _clock.UtcNow, cancellationToken);
                revoked = [session.Id];
            }
        }
        else revoked = await _sessions.RevokeAllForUserAsync(userId, _clock.UtcNow, cancellationToken);

        foreach (var sessionId in revoked) await _revocation.RevokeAsync(sessionId, cancellationToken);

        if (_tenantContext.Current?.TenantId is { } tenantId)
            await _audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(userId).Entity("session").Action("logout").After(new { revokedCount = revoked.Count }).Build(), cancellationToken);
    }

    public async Task<MeResult> MeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found");
        var memberships = await _memberships.ListActiveByUserAsync(userId, cancellationToken);
        var profile = new ProfileView(user.Id, user.Email, user.Name, user.PreferredLocale, user.MustChangePassword);
        return new MeResult(profile, ToSummaries(memberships, user.MustChangePassword));
    }

    public async Task<IReadOnlyList<AuthSessionView>> ListSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        var sessions = await _sessions.ListActiveAsync(userId, _clock.UtcNow, cancellationToken);
        return sessions.Select(s => new AuthSessionView(s.Id, s.DeviceLabel, s.Ip, s.CreatedAt, s.LastSeenAt, s.Id == currentSessionId)).ToList();
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var revoked = await _sessions.RevokeOwnedAsync(sessionId, userId, _clock.UtcNow, cancellationToken);
        if (!revoked) throw new NotFoundException("Session not found");

        await _revocation.RevokeAsync(sessionId, cancellationToken);
        if (_tenantContext.Current?.TenantId is { } tenantId)
            await _audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(userId).Entity("session").EntityId(sessionId).Action("revoke_session").Build(), cancellationToken);
    }

    public async Task RevokeAllOtherSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> revoked = currentSessionId is { } keep
            ? await _sessions.RevokeAllExceptAsync(userId, keep, _clock.UtcNow, cancellationToken)
            : await _sessions.RevokeAllForUserAsync(userId, _clock.UtcNow, cancellationToken);

        foreach (var sessionId in revoked) await _revocation.RevokeAsync(sessionId, cancellationToken);

        if (revoked.Count > 0 && _tenantContext.Current?.TenantId is { } tenantId)
            await _audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(userId).Entity("session").Action("revoke_sessions").After(new { revokedCount = revoked.Count }).Build(), cancellationToken);
    }
}
