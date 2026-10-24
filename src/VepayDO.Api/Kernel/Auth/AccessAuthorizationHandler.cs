using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Auth;

public sealed record AccessRequirement(string Permission) : IAuthorizationRequirement;

public sealed class AccessAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Resource is not HttpContext httpContext)
        {

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
