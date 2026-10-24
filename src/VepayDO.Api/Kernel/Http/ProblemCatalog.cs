using Microsoft.AspNetCore.Http;

namespace VepayDO.Api.Kernel.Http;

public static class ProblemCatalog
{
    public static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "BAD_REQUEST",
        StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
        StatusCodes.Status403Forbidden => "FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT_FOUND",
        StatusCodes.Status405MethodNotAllowed => "METHOD_NOT_ALLOWED",
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status413PayloadTooLarge => "PAYLOAD_TOO_LARGE",
        StatusCodes.Status415UnsupportedMediaType => "UNSUPPORTED_MEDIA_TYPE",
        StatusCodes.Status422UnprocessableEntity => "UNPROCESSABLE_ENTITY",
        StatusCodes.Status429TooManyRequests => "TOO_MANY_REQUESTS",
        StatusCodes.Status500InternalServerError => "INTERNAL_SERVER_ERROR",
        StatusCodes.Status502BadGateway => "BAD_GATEWAY",
        StatusCodes.Status503ServiceUnavailable => "SERVICE_UNAVAILABLE",
        StatusCodes.Status504GatewayTimeout => "GATEWAY_TIMEOUT",
        _ => $"HTTP_{status}",
    };

    public static (string Code, string I18nKey) DefaultsFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ("HTTP.BAD_REQUEST", "error.bad_request"),
        StatusCodes.Status401Unauthorized => ("HTTP.UNAUTHORIZED", "error.unauthorized"),
        StatusCodes.Status403Forbidden => ("HTTP.FORBIDDEN", "error.forbidden"),
        StatusCodes.Status404NotFound => ("HTTP.NOT_FOUND", "error.not_found"),
        StatusCodes.Status405MethodNotAllowed => ("HTTP.METHOD_NOT_ALLOWED", "error.method_not_allowed"),
        StatusCodes.Status409Conflict => ("HTTP.CONFLICT", "error.conflict"),
        StatusCodes.Status413PayloadTooLarge => ("HTTP.PAYLOAD_TOO_LARGE", "error.payload_too_large"),
        StatusCodes.Status415UnsupportedMediaType => ("HTTP.UNSUPPORTED_MEDIA_TYPE", "error.unsupported_media_type"),
        StatusCodes.Status422UnprocessableEntity => ("HTTP.UNPROCESSABLE_ENTITY", "error.unprocessable_entity"),
        StatusCodes.Status429TooManyRequests => ("HTTP.TOO_MANY_REQUESTS", "error.too_many_requests"),
        StatusCodes.Status500InternalServerError => ("HTTP.INTERNAL_SERVER_ERROR", "error.internal_server_error"),
        StatusCodes.Status502BadGateway => ("HTTP.BAD_GATEWAY", "error.bad_gateway"),
        StatusCodes.Status503ServiceUnavailable => ("HTTP.SERVICE_UNAVAILABLE", "error.service_unavailable"),
        StatusCodes.Status504GatewayTimeout => ("HTTP.GATEWAY_TIMEOUT", "error.gateway_timeout"),
        _ => ($"HTTP.{status}", "error.generic"),
    };

    public static string DetailFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "The request is invalid",
        StatusCodes.Status401Unauthorized => "Authentication required",
        StatusCodes.Status403Forbidden => "Operation not allowed",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status405MethodNotAllowed => "Method not allowed",
        StatusCodes.Status409Conflict => "The resource is in a conflicting state",
        StatusCodes.Status413PayloadTooLarge => "The request payload is too large",
        StatusCodes.Status415UnsupportedMediaType => "Unsupported media type",
        StatusCodes.Status422UnprocessableEntity => "The request could not be processed",
        StatusCodes.Status429TooManyRequests => "Too many requests",
        StatusCodes.Status500InternalServerError => "Internal server error",
        StatusCodes.Status502BadGateway => "Bad gateway",
        StatusCodes.Status503ServiceUnavailable => "Service unavailable",
        StatusCodes.Status504GatewayTimeout => "Gateway timeout",
        _ => "Request failed",
    };
}
