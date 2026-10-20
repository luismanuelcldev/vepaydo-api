using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

/// <summary>
/// Maps every unhandled failure to the RFC 9457 problem-details document of
/// <c>ProblemJson</c>. Expected domain failures keep their stable
/// <c>code</c>/<c>i18nKey</c>; anything unexpected becomes a 500 that never
/// leaks its message (AGENTS.md sections 3.2 and 13).
/// </summary>
public sealed partial class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody left to answer.
            return false;
        }

        var status = StatusOf(exception);

        if (exception is DomainException domain)
        {
            LogDomainFailure(domain.Code, status, httpContext.Request.Path.Value);

            await ProblemJson.WriteAsync(
                httpContext,
                status,
                domain.Code,
                domain.I18nKey,
                domain.Message,
                (exception as ValidationException)?.Errors,
                cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (exception is BadHttpRequestException badRequest)
        {
            await ProblemJson.WriteAsync(
                httpContext,
                badRequest.StatusCode,
                code: null,
                i18nKey: null,
                detail: "The request could not be read",
                errors: null,
                cancellationToken).ConfigureAwait(false);

            return true;
        }

        LogUnhandledException(exception, httpContext.Request.Method, httpContext.Request.Path.Value);

        await ProblemJson.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            code: null,
            i18nKey: null,
            detail: "Internal server error",
            errors: null,
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static int StatusOf(Exception exception) => exception switch
    {
        DomainException domain => domain.HttpStatus,
        BadHttpRequestException badRequest => badRequest.StatusCode,
        _ => StatusCodes.Status500InternalServerError,
    };

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Request failed with {ErrorCode} ({Status}): {Path}")]
    private partial void LogDomainFailure(string errorCode, int status, string? path);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    private partial void LogUnhandledException(Exception exception, string method, string? path);
}
