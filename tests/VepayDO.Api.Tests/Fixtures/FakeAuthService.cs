using VepayDO.Application.Auth;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeAuthService : IAuthService
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; } = Guid.NewGuid();

    public string AccessToken { get; set; } = "fake-access-token";

    public string RefreshToken { get; set; } = "fake-refresh-token";

    public DateTimeOffset RefreshExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(30);

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
}
