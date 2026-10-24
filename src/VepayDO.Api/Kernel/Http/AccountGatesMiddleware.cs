using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Legal;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

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
