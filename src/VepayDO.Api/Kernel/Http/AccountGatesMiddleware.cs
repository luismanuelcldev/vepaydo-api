using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Legal;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

/// <summary>
/// Account gates that run after authentication and before authorization,
/// exactly like the guard order of the reference API: a one-time temporary
/// password blocks everything until it is changed, and pending legal acceptance
/// blocks everything except the handful of routes needed to accept it
/// (AGENTS.md sections 9.9 and 12.2, UC-AUTH-08).
/// </summary>
public sealed class AccountGatesMiddleware
{
    private readonly RequestDelegate _next;

    public AccountGatesMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILegalService legalService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(legalService);

        var endpoint = context.GetEndpoint();
        if (endpoint is null ||
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var user = context.User.ToAuthenticatedUser();
        if (user is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (user.MustChangePassword &&
            endpoint.Metadata.GetMetadata<AllowPasswordChangeAttribute>() is null)
        {
            throw new PasswordChangeRequiredException();
        }

        if (endpoint.Metadata.GetMetadata<AllowPendingLegalAttribute>() is null &&
            !await legalService.HasAcceptedAllAsync(user.UserId, context.RequestAborted).ConfigureAwait(false))
        {
            throw new LegalAcceptanceRequiredException();
        }

        await _next(context).ConfigureAwait(false);
    }
}
