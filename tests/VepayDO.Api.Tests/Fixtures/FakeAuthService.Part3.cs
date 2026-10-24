using VepayDO.Application.Auth;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeAuthService
{

    public Task ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default)
    {
        LastResetToken = rawToken;
        LastResetPassword = newPassword;

        return ResetPasswordError is not null
            ? Task.FromException(ResetPasswordError)
            : Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuthSessionView>> ListSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Sessions);

    public Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RevokeAllOtherSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        RevokeAllCalls++;
        return Task.CompletedTask;
    }

    public Task IssuePasswordResetAsync(
        Guid userId,
        string email,
        PasswordResetOptions options,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private AuthResult NewAuthResult() => new(
        UserId,
        [new MembershipSummary(TenantId, "Mi tienda", MemberRole.OWNER, false)],
        AccessToken,
        RefreshToken,
        RefreshExpiresAt);
}
