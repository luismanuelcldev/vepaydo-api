using System.Security.Claims;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Auth;

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
