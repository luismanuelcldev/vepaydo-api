using VepayDO.Api.Kernel.Http;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VepayDO.Api.Kernel.Auth;

namespace VepayDO.Api.Extensions;

public static class RateLimiterExtensions
{
    public static IServiceCollection AddVepayDORateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authRateLimit = configuration.GetValue("App:AuthRateLimit", 10);
        var authStrictRateLimit = configuration.GetValue("App:AuthStrictRateLimit", 5);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => FixedWindow(context, permitLimit: authRateLimit));
            options.AddPolicy("auth-strict", context => FixedWindow(context, permitLimit: authStrictRateLimit));
            options.OnRejected = async (context, cancellationToken) => await ProblemJson.WriteAsync(
                context.HttpContext,
                StatusCodes.Status429TooManyRequests,
                code: "DOMAIN.TOO_MANY_REQUESTS",
                i18nKey: "error.too_many_requests",
                detail: "Too many requests",
                errors: null,
                cancellationToken).ConfigureAwait(false);
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(HttpContext context, int permitLimit)
    {
        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }
}
