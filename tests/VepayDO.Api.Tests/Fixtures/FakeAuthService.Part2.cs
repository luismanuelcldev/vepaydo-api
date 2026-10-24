using VepayDO.Application.Auth;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeAuthService
{

    public Task<AuthResult> LoginAsync(LoginInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return LoginError is not null
            ? Task.FromException<AuthResult>(LoginError)
            : Task.FromResult(NewAuthResult());
    }

    public Task<SelectTenantResult> SelectTenantAsync(
        Guid userId,
        Guid tenantId,
        string? refreshToken,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        LastSelectedTenantId = tenantId;

        if (SelectTenantError is not null)
        {
            return Task.FromException<SelectTenantResult>(SelectTenantError);
        }

        IReadOnlyList<MembershipSummary> memberships =
            [new MembershipSummary(TenantId, "Mi tienda", MemberRole.OWNER, false)];

        return Task.FromResult(new SelectTenantResult(AccessToken, memberships));
    }

    public Task<TokenPair> RefreshAsync(string rawRefreshToken, RequestMeta meta, CancellationToken cancellationToken = default) =>
        RefreshError is not null
            ? Task.FromException<TokenPair>(RefreshError)
            : Task.FromResult(new TokenPair(AccessToken, RefreshToken, RefreshExpiresAt));

    public Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default)
    {
        LogoutCalls++;
        return Task.CompletedTask;
    }

    public Task<MeResult> MeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MeResult(
            new ProfileView(UserId, "owner@example.test", "Owner", "es-DO", false),
            [new MembershipSummary(TenantId, "Mi tienda", MemberRole.OWNER, false)]));

    public Task<string> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        LastChangePasswordCurrent = currentPassword;

        return ChangePasswordError is not null
            ? Task.FromException<string>(ChangePasswordError)
            : Task.FromResult(AccessToken);
    }

    public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default)
    {
        LastLocale = preferredLocale;
        return Task.CompletedTask;
    }

    public Task ForgotPasswordAsync(string email, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        LastForgotEmail = email;

        return ForgotPasswordError is not null
            ? Task.FromException(ForgotPasswordError)
            : Task.CompletedTask;
    }
}
