using Serilog.Context;
using VepayDO.Api.Kernel.Auth;

namespace VepayDO.Api.Kernel.Logging;

public sealed class RequestLogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLogEnrichmentMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var traceId = context.TraceIdentifier;
        var clientIp = context.Connection.RemoteIpAddress?.ToString();

        var user = context.User.ToAuthenticatedUser();

        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("ClientIp", clientIp))
        using (LogContext.PushProperty("TenantId", user?.TenantId))
        using (LogContext.PushProperty("UserId", user?.UserId))
        {
            await _next(context).ConfigureAwait(false);
        }
    }
}
