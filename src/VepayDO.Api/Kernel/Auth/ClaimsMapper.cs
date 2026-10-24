using System.Security.Claims;
using VepayDO.Application.Security;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Kernel.Auth;

public static class ClaimsMapper
{
    public static AuthenticatedUser? ToAuthenticatedUser(this ClaimsPrincipal? principal)
    {
        if (principal is null || principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (!Guid.TryParse(principal.FindFirstValue(TokenConstants.SubjectClaim), out var userId) ||
            userId == Guid.Empty)
        {
            return null;
        }

        var sessionId = Guid.TryParse(principal.FindFirstValue(TokenConstants.SessionIdClaim), out var sid)
            ? (Guid?)sid
            : null;

        var tenantId = Guid.TryParse(principal.FindFirstValue(TokenConstants.TenantIdClaim), out var tid)
            ? (Guid?)tid
            : null;

        var roleValue = principal.FindFirstValue(TokenConstants.RoleClaim);
        MemberRole? role = tenantId is null
            ? null
            : roleValue switch
            {
                "OWNER" => MemberRole.OWNER,
                "VERIFIER" => MemberRole.VERIFIER,
                _ => null,
            };

        if (tenantId is not null && role is null)
        {
            return null;
        }

        return new AuthenticatedUser
        {
            UserId = userId,
            SessionId = sessionId,
            TenantId = tenantId,
            Role = role,
            MustChangePassword = string.Equals(
                principal.FindFirstValue(TokenConstants.PasswordChangeClaim),
                TokenConstants.PasswordChangeTrue,
                StringComparison.OrdinalIgnoreCase),
        };
    }

    public static Guid? SessionIdOf(this ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirstValue(TokenConstants.SessionIdClaim), out var sessionId)
            ? sessionId
            : null;
}
