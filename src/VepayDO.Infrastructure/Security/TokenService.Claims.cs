using System.Security.Claims;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Infrastructure.Security;

public sealed partial class TokenService
{
    private static IEnumerable<Claim> BuildClaims(AccessTokenClaims claims)
    {
        yield return new Claim(TokenConstants.SubjectClaim, claims.UserId.ToString("D"));
        yield return new Claim(TokenConstants.TypeClaim, TokenConstants.TokenTypeAccess);

        if (claims.SessionId is { } sessionId)
            yield return new Claim(TokenConstants.SessionIdClaim, sessionId.ToString("D"));

        if (claims.TenantId is { } tenantId)
            yield return new Claim(TokenConstants.TenantIdClaim, tenantId.ToString("D"));

        if (!string.IsNullOrEmpty(claims.Role))
            yield return new Claim(TokenConstants.RoleClaim, claims.Role);

        if (claims.MustChangePassword)
            yield return new Claim(TokenConstants.PasswordChangeClaim, TokenConstants.PasswordChangeTrue);
    }

    private static AccessTokenClaims MapClaims(IDictionary<string, object> claims)
    {
        if (!TryReadGuid(claims, TokenConstants.SubjectClaim, out var userId) || userId == Guid.Empty)
        {
            throw new UnauthorizedException("Access token has no subject");
        }

        var sessionId = TryReadGuid(claims, TokenConstants.SessionIdClaim, out var parsedSession) ? parsedSession : (Guid?)null;
        var tenantId = TryReadGuid(claims, TokenConstants.TenantIdClaim, out var parsedTenant) ? parsedTenant : (Guid?)null;

        var role = claims.TryGetValue(TokenConstants.RoleClaim, out var roleValue) ? roleValue?.ToString() : null;
        var mustChangePassword = claims.TryGetValue(TokenConstants.PasswordChangeClaim, out var pwd) &&
            string.Equals(pwd?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        return new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            TenantId = tenantId,
            Role = role,
            MustChangePassword = mustChangePassword,
        };
    }

    private static bool TryReadGuid(IDictionary<string, object> claims, string claimType, out Guid value)
    {
        value = Guid.Empty;
        if (!claims.TryGetValue(claimType, out var raw)) return false;
        return Guid.TryParse(raw?.ToString(), out value);
    }
}
