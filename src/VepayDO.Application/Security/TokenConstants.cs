namespace VepayDO.Application.Security;

public static class TokenConstants
{

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public const string SubjectClaim = "sub";

    public const string TypeClaim = "typ";

    public const string TokenTypeAccess = "access";

    public const string SessionIdClaim = "sid";

    public const string TenantIdClaim = "tid";

    public const string RoleClaim = "role";

    public const string PasswordChangeClaim = "pwd";

    public const string PasswordChangeTrue = "true";
}
