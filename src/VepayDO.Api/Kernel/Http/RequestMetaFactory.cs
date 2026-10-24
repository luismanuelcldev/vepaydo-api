using Microsoft.AspNetCore.Http;

using VepayDO.Application.Auth;

namespace VepayDO.Api.Kernel.Http;

/// <summary>
/// Builds the <see cref="RequestMeta"/> captured from the HTTP boundary for
/// auditing and session bookkeeping, so every controller derives the caller's
/// IP, user agent, and truncated device label the same way (AGENTS.md section 13).
/// </summary>
public static class RequestMetaFactory
{
    public static RequestMeta From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userAgent = context.Request.Headers.UserAgent.Count > 0
            ? context.Request.Headers.UserAgent.ToString()
            : null;

        var deviceLabel = userAgent is null
            ? null
            : userAgent[..Math.Min(userAgent.Length, AuthConstants.DeviceLabelMaxLength)];

        return new RequestMeta(context.Connection.RemoteIpAddress, userAgent, deviceLabel);
    }
}