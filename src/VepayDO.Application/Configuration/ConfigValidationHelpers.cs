using VepayDO.Domain.Errors;

namespace VepayDO.Application.Configuration;

internal static class ConfigValidationHelpers
{
    public static void RequiredValue(string? value, string path, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add(new FieldError(path, "is required"));
    }

    public static void RequiredUrl(string? value, string path, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new FieldError(path, "is required"));
            return;
        }
        if (!IsAbsoluteUrl(value)) errors.Add(new FieldError(path, "must be an absolute URL"));
    }

    public static void OptionalUrl(string? value, string path, List<FieldError> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsAbsoluteUrl(value))
            errors.Add(new FieldError(path, "must be an absolute URL when set"));
    }

    public static void MinLength(string? value, int minLength, string path, List<FieldError> errors)
    {
        if (string.IsNullOrEmpty(value) || value.Length < minLength)
            errors.Add(new FieldError(path, $"must be at least {minLength} characters"));
    }

    private static bool IsAbsoluteUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Scheme);
}
