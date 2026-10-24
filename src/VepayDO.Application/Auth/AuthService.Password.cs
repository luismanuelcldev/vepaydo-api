using VepayDO.Application.Audit;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService
{
    public async Task<string> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, Guid? sessionId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found");
        if (!_hasher.Verify(user.PasswordHash, currentPassword)) throw new UnauthorizedException("The current password is incorrect");

        await _users.UpdatePasswordAsync(userId, _hasher.Hash(newPassword), false, cancellationToken);
        var memberships = await _memberships.ListActiveByUserAsync(userId, cancellationToken);
        var selected = memberships.Count > 0 ? memberships[0] : null;

        var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
        {
            UserId = userId, SessionId = sessionId, TenantId = selected?.TenantId ?? _tenantContext.Current?.TenantId, Role = selected?.Role.ToString(), MustChangePassword = false,
        });

        if (selected?.TenantId is { } tenantId)
            await _audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(userId).Entity("user").EntityId(userId).Action("change_password").Build(), cancellationToken);

        return accessToken;
    }

    public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default) =>
        _users.UpdateLocaleAsync(userId, preferredLocale, cancellationToken);

    public async Task ForgotPasswordAsync(string email, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);
        var normalized = NormalizeEmail(email);

        if (meta.Ip is { } ip && !_resetLimiter.TryConsumeIp(ip.ToString())) throw new TooManyRequestsException();
        if (!_resetLimiter.TryConsumeEmail(normalized)) throw new TooManyRequestsException();

        var user = await InNoneTxAsync(async ct => await _users.FindByEmailAsync(normalized, ct), cancellationToken);
        if (user is null || !user.IsActive || _resetLimiter.IsWithinCooldown(normalized)) return;

        await IssuePasswordResetAsync(user.Id, user.Email, new PasswordResetOptions(AuthConstants.ResetTokenLifetime, meta.Ip, null), cancellationToken);
        _resetLimiter.MarkIssued(normalized);
    }
}
