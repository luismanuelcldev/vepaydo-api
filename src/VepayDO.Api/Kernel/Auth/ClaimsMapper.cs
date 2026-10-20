using System.Security.Claims;
using VepayDO.Application.Security;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Kernel.Auth;

/// <summary>
/// Turns the claims of a verified access token into the request identity.
/// Anything that does not parse fails closed: an unrecognised member role, a
/// missing subject, or a non-authenticated principal all return <c>null</c> and
/// the request is treated as unauthenticated (AGENTS.md section 7).
/// </summary>
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

        // A tenant without a known role would reach the database with an empty
        // app.role and fail closed anyway; refuse it here instead.
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
