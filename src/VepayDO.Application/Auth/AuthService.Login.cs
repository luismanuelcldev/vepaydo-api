using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService
{
    public async Task<AuthResult> LoginAsync(LoginInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var email = NormalizeEmail(input.Email);
        if (await _lockout.IsBlockedAsync(email, cancellationToken))
            throw new TooManyRequestsException("Too many failed attempts. Try again later.");

        var user = await InNoneTxAsync(async ct => await _users.FindByEmailAsync(email, ct), cancellationToken);
        if (user is null || !user.IsActive || !_hasher.Verify(user.PasswordHash, input.Password))
        {
            await _lockout.RegisterFailureAsync(email, cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        await _lockout.ResetAsync(email, cancellationToken);

        return await _runner.RunAsUserAsync(user.Id, async ct =>
        {
            var active = await _memberships.ListActiveByUserAsync(user.Id, ct);
            if (active.Count == 0) throw new ForbiddenException("The account has no active business membership");

            await _users.UpdateLastLoginAsync(user.Id, _clock.UtcNow, ct);
            var selected = active.Count == 1 ? active[0] : null;
            var session = await CreateSessionAsync(user.Id, selected?.TenantId, meta, ct);
            var accessToken = _tokens.SignAccessToken(new AccessTokenClaims
            {
                UserId = user.Id, SessionId = session.SessionId, TenantId = selected?.TenantId, Role = selected?.Role.ToString(), MustChangePassword = user.MustChangePassword,
            });

            return new AuthResult(user.Id, ToSummaries(active, user.MustChangePassword), accessToken, session.RefreshToken, session.RefreshExpiresAt);
        }, cancellationToken);
    }
}
