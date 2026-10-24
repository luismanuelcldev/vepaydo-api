using Microsoft.AspNetCore.Authorization;
using VepayDO.Application.Security;

namespace VepayDO.Api.Kernel.Auth;

public static class AuthorizationPolicyRegistrationExtensions
{
    public static AuthorizationOptions AddVepayDOPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var permission in Permissions.All)
        {
            options.AddPolicy(permission, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AccessRequirement(permission));
            });
        }

        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        return options;
    }
}
