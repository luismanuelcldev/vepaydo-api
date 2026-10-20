using VepayDO.Application.Auth;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Tests.Fixtures;

/// <summary>
/// Configuration-driven stand-in for <see cref="IAuthService"/>. The real service
/// needs PostgreSQL and argon2; these tests exercise the HTTP contract (verbs,
/// status codes, cookie handling) and the pipeline around it, so the use cases
/// are replaced by canned results. Set <see cref="Configure"/> to throw for the
/// negative paths.
/// </summary>
public sealed class FakeAuthService : IAuthService
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; } = Guid.NewGuid();

    public string AccessToken { get; set; } = "fake-access-token";

    public string RefreshToken { get; set; } = "fake-refresh-token";

    public DateTimeOffset RefreshExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(30);

    /// <summary>When set, the matching use case throws instead of returning a result.</summary>
    public Exception? RegisterError { get; set; }

    public Exception? LoginError { get; set; }

    public Exception? RefreshError { get; set; }

    public Exception? SelectTenantError { get; set; }

    public Exception? ChangePasswordError { get; set; }

    public Exception? ForgotPasswordError { get; set; }

    public Exception? ResetPasswordError { get; set; }

    public string? LastForgotEmail { get; private set; }

    public string? LastResetToken { get; private set; }

    public string? LastResetPassword { get; private set; }

    public string? LastLocale { get; private set; }

    public Guid? LastSelectedTenantId { get; private set; }

    public string? LastChangePasswordCurrent { get; private set; }

    public int LogoutCalls { get; private set; }

    public int RevokeAllCalls { get; private set; }

    public IReadOnlyList<AuthSessionView> Sessions { get; set; } = [];

    /// <summary>
    /// Clears every mutable field. The factory hosts one instance shared by all
    /// tests in a class, so tests call this in their constructor to stay
    /// independent when xUnit runs them in an unpredictable order.
    /// </summary>
    public void Reset()
    {
        RegisterError = null;
        LoginError = null;
        RefreshError = null;
        SelectTenantError = null;
        ChangePasswordError = null;
        ForgotPasswordError = null;
        ResetPasswordError = null;
        LastForgotEmail = null;
        LastResetToken = null;
        LastResetPassword = null;
        LastLocale = null;
        LastSelectedTenantId = null;
        LastChangePasswordCurrent = null;
        LogoutCalls = 0;
        RevokeAllCalls = 0;
        Sessions = [];
    }

    public Task<AuthResult> RegisterAsync(RegisterInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RegisterError is not null
            ? Task.FromException<AuthResult>(RegisterError)
            : Task.FromResult(NewAuthResult());
    }

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
