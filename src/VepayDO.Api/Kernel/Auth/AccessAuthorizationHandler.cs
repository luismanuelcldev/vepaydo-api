using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Auth;

/// <summary>
/// The capability a policy requires. Registered with one policy per code in
/// <c>Kernel/Auth/AuthorizationPolicyRegistrationExtensions</c>.
/// </summary>
public sealed record AccessRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// The single authorization handler for every policy: it checks the permissions
/// matrix of AGENTS.md section 9.5 against the member role carried by the
/// access token. There is no other authorization logic in the API, and the
/// database RLS remains the last line of defence (section 13).
/// </summary>
public sealed class AccessAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Resource is not HttpContext httpContext)
        {
            // Only the endpoint middleware supplies the HttpContext; without it
            // there is nothing to authorize against, so fail closed.
            context.Fail();
            return Task.CompletedTask;
        }

        var user = httpContext.User.ToAuthenticatedUser();
        if (user is null)
        {
            throw new UnauthorizedException();
        }

        foreach (var requirement in context.Requirements)
        {
            if (requirement is not AccessRequirement accessRequirement)
            {
                // The "authenticated" requirement: already satisfied above.
                context.Succeed(requirement);
                continue;
            }

            if (!PermissionPolicy.Can(user.Role, accessRequirement.Permission))
            {
                throw new ForbiddenException($"Missing permission: {accessRequirement.Permission}");
            }

            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
