using Microsoft.AspNetCore.Authorization;

namespace VepayDO.Api.Kernel.Attributes;

/// <summary>
/// Marks a route (or a controller) as reachable without an access token. It
/// implements both <see cref="IAllowAnonymous"/> (endpoint authorization) and
/// <see cref="IAllowAnonymousFilter"/> (MVC authorization filters) so the
/// pipeline and the framework agree, exactly like the reference
/// <c>@Public()</c> decorator.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PublicAttribute
    : Attribute, IAllowAnonymous, Microsoft.AspNetCore.Mvc.Authorization.IAllowAnonymousFilter { }