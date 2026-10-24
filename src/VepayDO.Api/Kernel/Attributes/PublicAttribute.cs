using Microsoft.AspNetCore.Authorization;

namespace VepayDO.Api.Kernel.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PublicAttribute
    : Attribute, IAllowAnonymous, Microsoft.AspNetCore.Mvc.Authorization.IAllowAnonymousFilter { }
