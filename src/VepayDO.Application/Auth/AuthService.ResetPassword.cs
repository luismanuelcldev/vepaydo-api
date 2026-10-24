using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Notifications;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService
{
    public async Task ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rawToken)) throw new UnauthorizedException("Invalid or expired reset token");
        var tokenHash = Sha256(rawToken);
        var token = await InNoneTxAsync(async ct => await _resetTokens.FindByTokenHashAsync(tokenHash, ct), cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= _clock.UtcNow)
            throw new UnauthorizedException("Invalid or expired reset token");

        var passwordHash = _hasher.Hash(newPassword);
        var revoked = await _runner.RunAsUserAsync(token.UserId, async ct =>
        {
            await _users.UpdatePasswordAsync(token.UserId, passwordHash, false, ct);
            await _resetTokens.MarkUsedAsync(token.Id, _clock.UtcNow, ct);
            return await _sessions.RevokeAllForUserAsync(token.UserId, _clock.UtcNow, ct);
        }, cancellationToken);

        foreach (var sessionId in revoked) await _revocation.RevokeAsync(sessionId, cancellationToken);

        var tenantIds = await _runner.RunAsUserAsync(token.UserId, async ct =>
        {
            var active = await _memberships.ListActiveByUserAsync(token.UserId, ct);
            return (IReadOnlyList<Guid>)active.Select(m => m.TenantId).ToList();
        }, cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            await _runner.RunAsSystemAsync(tenantId, async ct =>
            {
                await _audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(token.UserId).Actor(AuditActor.SYSTEM).Entity("user").EntityId(token.UserId).Action("reset_password").Build(), ct);
                return true;
            }, cancellationToken);
        }

        var email = await InNoneTxAsync(async ct => (await _users.FindByIdAsync(token.UserId, ct))?.Email, cancellationToken);
        if (email is not null) await _mail.SendAsync(MailTemplates.PasswordChanged(email, _config.BrandName), cancellationToken);
    }

    public async Task IssuePasswordResetAsync(Guid userId, string email, PasswordResetOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var token = NewOpaqueToken();
        var tokenHash = Sha256(token);
        var expiresAt = _clock.UtcNow + options.Lifetime;

        await InUserTxAsync(userId, async ct =>
        {
            await _resetTokens.InvalidateUnusedForUserAsync(userId, _clock.UtcNow, ct);
            await _resetTokens.CreateAsync(new NewPasswordResetToken(userId, tokenHash, expiresAt, options.RequestedIp, options.IssuedBySupportId), ct);
            return true;
        }, cancellationToken);

        var resetUrl = $"{_config.AppOrigin}/reset-password?token={token}";
        await _mail.SendAsync(MailTemplates.PasswordReset(email, resetUrl, _config.BrandName), cancellationToken);
    }
}
