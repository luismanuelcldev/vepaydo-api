using Microsoft.AspNetCore.Http;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Configuration;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

public sealed class OriginGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedOrigin;

    public OriginGuardMiddleware(RequestDelegate next, AppConfig appConfig)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(appConfig);

        _next = next;
        _expectedOrigin = appConfig.AppOrigin.TrimEnd('/');
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.GetEndpoint()?.Metadata.GetMetadata<CheckOriginAttribute>() is null
            || ClientTypeHeaders.IsNative(context.Request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var requestOrigin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(requestOrigin))
        {
            if (!string.Equals(requestOrigin.TrimEnd('/'), _expectedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Request origin is not allowed");
            }
        }
        else
        {
            var referer = context.Request.Headers.Referer.ToString();
            if (string.IsNullOrEmpty(referer) ||
                !referer.StartsWith(_expectedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Request origin is not allowed");
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
