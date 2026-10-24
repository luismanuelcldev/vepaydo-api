using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Tenancy;

namespace VepayDO.Api.Kernel.Http;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantTransactionRunner runner)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runner);

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var tenantContext = Resolve(context);
        if (tenantContext is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await runner.RunAsync(
            tenantContext,
            async ct =>
            {
                await _next(context).ConfigureAwait(false);
                return true;
            },
            context.RequestAborted).ConfigureAwait(false);
    }

    private static TenantContext? Resolve(HttpContext context)
    {
        var user = context.User.ToAuthenticatedUser();
        if (user is null)
        {
            return null;
        }

        if (user.TenantId is null)
        {
            return TenantContext.ForUser(user.UserId);
        }

        var role = user.Role?.ToDbRole();
        return role is null
            ? null
            : TenantContext.ForTenant(user.TenantId.Value, user.UserId, role.Value);
    }
}
