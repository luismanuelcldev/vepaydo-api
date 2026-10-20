using Serilog.Context;
using VepayDO.Api.Kernel.Auth;

namespace VepayDO.Api.Kernel.Logging;

/// <summary>
/// Pushes request-scoped diagnostic properties onto every log line written while
/// the request runs (AGENTS.md section 13): the trace id, the authenticated
/// tenant and user, and the client IP. Because these come from the verified
/// token and the connection, they add correlation without leaking content.
/// The values are attached as structured properties so JSON logs can be filtered
/// by <c>TenantId</c> or <c>TraceId</c>.
/// </summary>
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
