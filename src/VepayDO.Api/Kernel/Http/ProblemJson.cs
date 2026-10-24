using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

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
