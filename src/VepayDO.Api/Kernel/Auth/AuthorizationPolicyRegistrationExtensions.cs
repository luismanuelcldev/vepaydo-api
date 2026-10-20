using Microsoft.AspNetCore.Authorization;
using VepayDO.Application.Security;

namespace VepayDO.Api.Kernel.Auth;

/// <summary>
/// Registers one authorization policy per capability code plus the fallback
/// policy that requires an authenticated caller on every endpoint that does not
/// say otherwise (AGENTS.md section 9.5, section 10).
/// </summary>
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
