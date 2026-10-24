namespace VepayDO.Domain.Errors;

public sealed record FieldError(string Path, string Message);

public sealed class ValidationException : DomainException
{
    public ValidationException(IReadOnlyList<FieldError> errors)
        : base(
            $"Validation failed: {string.Join(", ", errors.Select(e => $"{e.Path}: {e.Message}"))}",
            httpStatus: 422)
    {
        Errors = errors;
    }

    public override string Code => "DOMAIN.VALIDATION";

    public override string I18nKey => "error.validation";

    public IReadOnlyList<FieldError> Errors { get; }
}
