namespace VepayDO.Application.Auth;

public static class AuthConstants
{

    public const string RefreshCookieName = "vepaydo_refresh";

    public const string RefreshCookiePath = "/api/v1/auth";

    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan SupportResetTokenLifetime = TimeSpan.FromMinutes(15);

    public const int ForgotPasswordMaxPerEmail = 5;

    public const int ForgotPasswordMaxPerIp = 5;

    public static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromHours(1);

    public static readonly TimeSpan ForgotPasswordResendCooldown = TimeSpan.FromMinutes(1);

    public const string ForgotPasswordNeutralMessage =
        "Si el correo corresponde a una cuenta, enviamos un enlace para restablecer la contraseña.";

    public const int DeviceLabelMaxLength = 120;

    public const string DefaultLocale = "es-DO";
}
