namespace VepayDO.Application.Security;

/// <summary>
/// Claim names and lifetimes for the access token and the opaque rotating
/// refresh token (AGENTS.md section 13, reference <c>token.service.ts</c>).
/// </summary>
public static class TokenConstants
{
    /// <summary>Access token lifetime: 15 minutes.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Refresh token lifetime: 30 days, rotating.</summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public const string SubjectClaim = "sub";

    /// <summary>Token type; always <c>access</c> for the short-lived JWT.</summary>
    public const string TypeClaim = "typ";

    public const string TokenTypeAccess = "access";

    /// <summary>Session id; lets the guard reject revoked sessions.</summary>
    public const string SessionIdClaim = "sid";

    /// <summary>Present once a tenant has been selected.</summary>
    public const string TenantIdClaim = "tid";

    /// <summary>Present together with <c>tid</c>; <c>OWNER</c> or <c>VERIFIER</c>.</summary>
    public const string RoleClaim = "role";

    /// <summary>Forces a password change before the token can reach tenant routes.</summary>
    public const string PasswordChangeClaim = "pwd";

    /// <summary>The value of <see cref="PasswordChangeClaim"/> while a change is pending.</summary>
    public const string PasswordChangeTrue = "true";
}
