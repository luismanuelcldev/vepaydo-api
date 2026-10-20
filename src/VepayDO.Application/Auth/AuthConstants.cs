namespace VepayDO.Application.Auth;

/// <summary>
/// Stable auth constants and copy shared by every transport. Mirrors the
/// reference <c>auth.constants.ts</c> so the behavior of both APIs stays aligned
/// (AGENTS.md sections 9.9 and 13).
/// </summary>
public static class AuthConstants
{
    /// <summary>Name of the httpOnly refresh-token cookie.</summary>
    public const string RefreshCookieName = "vepaydo_refresh";

    /// <summary>Only sent to the auth endpoints; kept in sync with the API prefix.</summary>
    public const string RefreshCookiePath = "/api/v1/auth";

    /// <summary>User-initiated password-reset token lifetime (UC-AUTH-04).</summary>
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Operator-issued password-reset token lifetime (UC-AUTH-05).</summary>
    public static readonly TimeSpan SupportResetTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Max user-initiated reset requests per email per window.</summary>
    public const int ForgotPasswordMaxPerEmail = 5;

    /// <summary>Max user-initiated reset requests per IP per window (UC-AUTH-04).</summary>
    public const int ForgotPasswordMaxPerIp = 5;

    public static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromHours(1);

    /// <summary>Minimum delay before a new reset link is emailed for the same account.</summary>
    public static readonly TimeSpan ForgotPasswordResendCooldown = TimeSpan.FromMinutes(1);

    /// <summary>Neutral copy returned by forgot-password regardless of account existence.</summary>
    public const string ForgotPasswordNeutralMessage =
        "Si el correo corresponde a una cuenta, enviamos un enlace para restablecer la contraseña.";

    /// <summary>Longest device label stored for a session (UC-AUTH-06).</summary>
    public const int DeviceLabelMaxLength = 120;

    /// <summary>Default preferred locale for a new account (AGENTS.md section 12.4).</summary>
    public const string DefaultLocale = "es-DO";
}
