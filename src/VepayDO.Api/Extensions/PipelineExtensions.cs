using Serilog;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Health;
using VepayDO.Api.Kernel.Http;
using VepayDO.Api.Kernel.Logging;

namespace VepayDO.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UseVepayDOPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();

        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;
            await ProblemJson.WriteAsync(
                httpContext,
                httpContext.Response.StatusCode,
                code: null,
                i18nKey: null,
                detail: null,
                errors: null,
                httpContext.RequestAborted).ConfigureAwait(false);
        });

        app.UseRouting();
        app.UseMiddleware<OriginGuardMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<RequestLogEnrichmentMiddleware>();
        app.UseMiddleware<AccountGatesMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<TenantContextMiddleware>();

        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi("/api/v1/docs/openapi.json");
        }

        app.UseRateLimiter();
        app.MapControllers();
        app.MapHealthEndpoints();

        return app;
    }
}
