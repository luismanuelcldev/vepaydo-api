using Microsoft.AspNetCore.Http;

using VepayDO.Application.Auth;

namespace VepayDO.Api.Kernel.Http;

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
