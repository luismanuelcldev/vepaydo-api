using Microsoft.AspNetCore.Authorization;
using VepayDO.Application.Security;

namespace VepayDO.Api.Kernel.Attributes;

/// <summary>
/// Declares the capabilities a route requires. The codes are resolved against
/// the member's role through <see cref="PermissionPolicy"/>, the executable
/// permissions matrix of AGENTS.md section 9.5. Inherits
/// <see cref="AuthorizeAttribute"/> so the authorization middleware picks the
/// policy up from the endpoint metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);

        Permission = permission;
        Policy = permission;
    }

    /// <summary>The capability code this route requires.</summary>
    public string Permission { get; }
}
