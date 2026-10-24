using Microsoft.AspNetCore.Authorization;
using VepayDO.Application.Security;

namespace VepayDO.Api.Kernel.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);

        Permission = permission;
        Policy = permission;
    }

    public string Permission { get; }
}
