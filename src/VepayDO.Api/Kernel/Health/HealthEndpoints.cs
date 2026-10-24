using Npgsql;

namespace VepayDO.Api.Kernel.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health/live", () => Results.Ok(new HealthResponse("alive")))
            .AllowAnonymous()
            .WithName("health-live")
            .WithTags("Health");

        endpoints.MapGet("/health/ready", ReadyAsync)
            .AllowAnonymous()
            .WithName("health-ready")
            .WithTags("Health");

        return endpoints;
    }

    private static async Task<IResult> ReadyAsync(
        HttpContext context,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("VepayDO.Api.Health");

        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is not null)
            {
                return Results.Ok(new HealthResponse("ready"));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HealthLog.DatabaseUnreachable(logger, exception);
        }

        return Results.Json(
            new
            {
                type = "about:blank",
                title = "SERVICE_UNAVAILABLE",
                status = StatusCodes.Status503ServiceUnavailable,
                detail = "Database is not reachable",
                instance = context.Request.Path.Value,
                code = "HEALTH.DATABASE_UNAVAILABLE",
                i18nKey = "health.database_unavailable",
                traceId = context.TraceIdentifier,
                timestamp = DateTimeOffset.UtcNow,
            },
            statusCode: StatusCodes.Status503ServiceUnavailable,
            contentType: "application/problem+json");
    }

    private sealed record HealthResponse(string Status);
}

internal static partial class HealthLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Error,
        Message = "Readiness probe failed: the database is not reachable")]
    public static partial void DatabaseUnreachable(ILogger logger, Exception exception);
}
