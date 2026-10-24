namespace VepayDO.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterInput input, RequestMeta meta, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(LoginInput input, RequestMeta meta, CancellationToken cancellationToken = default);

    Task<SelectTenantResult> SelectTenantAsync(
        Guid userId,
        Guid tenantId,
        string? refreshToken,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<TokenPair> RefreshAsync(string rawRefreshToken, RequestMeta meta, CancellationToken cancellationToken = default);

    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default);

    Task<MeResult> MeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<string> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        Guid? sessionId,
        CancellationToken cancellationToken = default);

    Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default);

    Task ForgotPasswordAsync(string email, RequestMeta meta, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthSessionView>> ListSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    Task RevokeAllOtherSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default);

    Task IssuePasswordResetAsync(
        Guid userId,
        string email,
        PasswordResetOptions options,
        CancellationToken cancellationToken = default);
}
