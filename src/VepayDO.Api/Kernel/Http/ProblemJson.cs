using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

/// <summary>
/// Writes the RFC 9457 problem-details document. Used by the exception handler
/// and by the authentication events, so every error leaving the API has one
/// shape: the standard members plus <c>code</c>, <c>i18nKey</c>, <c>traceId</c>
/// and optional field <c>errors</c> (AGENTS.md section 10).
/// </summary>
public static class ProblemJson
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string? code,
        string? i18nKey,
        string? detail,
        IReadOnlyList<FieldError>? errors = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Response.HasStarted)
        {
            return;
        }

        var defaults = ProblemCatalog.DefaultsFor(status);
        var body = new ProblemBody(
            Type: "about:blank",
            Title: ProblemCatalog.TitleFor(status),
            Status: status,
            Detail: detail ?? ProblemCatalog.DetailFor(status),
            Instance: context.Request.Path + context.Request.QueryString,
            Code: code ?? defaults.Code,
            I18nKey: i18nKey ?? defaults.I18nKey,
            TraceId: context.TraceIdentifier,
            Timestamp: DateTimeOffset.UtcNow,
            Errors: errors);

        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;
        await context.Response
            .WriteAsync(JsonSerializer.Serialize(body, SerializerOptions), cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record ProblemBody(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("detail")] string Detail,
        [property: JsonPropertyName("instance")] string Instance,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("i18nKey")] string I18nKey,
        [property: JsonPropertyName("traceId")] string TraceId,
        [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
        [property: JsonPropertyName("errors")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<FieldError>? Errors);
}
