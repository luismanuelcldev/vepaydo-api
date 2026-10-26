using Microsoft.AspNetCore.Http;

namespace VepayDO.Api.Kernel.Http;

/// <summary>
/// Constants and helpers for the <c>X-Client-Type</c> header, which identifies
/// non-browser (native) clients such as the Flutter mobile app. Native clients
/// have no browsable origin, and cookie-based CSRF protection is irrelevant for
/// them because they send tokens explicitly instead of relying on ambient
/// cookies (see ADR 0006).
/// </summary>
public static class ClientTypeHeaders
{
    public const string Name = "X-Client-Type";

    public const string Native = "native";

    /// <summary>
    /// Returns whether the request is explicitly marked as coming from a
    /// native (non-browser) client.
    /// </summary>
    public static bool IsNative(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(
            request.Headers[Name].ToString().Trim(),
            Native,
            StringComparison.OrdinalIgnoreCase);
    }
}
