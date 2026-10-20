using System.Security.Claims;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Auth;

/// <summary>
/// Convenience accessors over the request identity. Actions that already ran
/// through the authentication middleware can call <see cref="RequireUser"/> and
/// rely on the value; a missing identity is a programming error and fails as
/// <c>DOMAIN.UNAUTHORIZED</c> rather than a null reference.
/// </summary>
public static class CurrentUserExtensions
{
    public static AuthenticatedUser RequireUser(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.ToAuthenticatedUser() ?? throw new UnauthorizedException();
    }

    public static Guid RequireUserId(this ClaimsPrincipal principal) =>
        principal.RequireUser().UserId;

    public static Guid? SessionIdOrNull(this ClaimsPrincipal principal) =>
        principal.SessionIdOf();
}
