using Microsoft.AspNetCore.Http;
using VepayDO.Application.Auth;

namespace VepayDO.Api.Kernel.Http;

public static class AuthCookies
{
    public static void SetRefreshToken(
        HttpContext context,
        string token,
        DateTimeOffset expiresAt,
        bool secure)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Cookies.Append(
            AuthConstants.RefreshCookieName,
            token,
            options: new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                Path = AuthConstants.RefreshCookiePath,
                Expires = expiresAt,
            });
    }

    public static void ClearRefreshToken(HttpContext context, bool secure)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Cookies.Delete(
            AuthConstants.RefreshCookieName,
            options: new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                Path = AuthConstants.RefreshCookiePath,
            });
    }

    public static string? ReadRefreshToken(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Cookies[AuthConstants.RefreshCookieName];
    }
}
