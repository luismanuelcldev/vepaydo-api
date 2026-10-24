namespace VepayDO.Domain.Errors;

/// <summary>
/// Base class for expected, user-presentable domain failures. Every exception
/// exposes a stable English <see cref="Code"/> and an <see cref="I18nKey"/> the
/// presentation layer translates (AGENTS.md section 3.2).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(
        string message,
        int httpStatus = 400,
        Exception? innerException = null)
        : base(message, innerException)
    {
        HttpStatus = httpStatus;
    }

    public abstract string Code { get; }

    public abstract string I18nKey { get; }

    public int HttpStatus { get; }
}

public sealed class InvalidArgumentDomainException : DomainException
{
    public InvalidArgumentDomainException(string message)
        : base(message)
    {
    }

    public override string Code => "DOMAIN.INVALID_ARGUMENT";

    public override string I18nKey => "domain.invalid_argument";
}

public sealed class MissingTenantContextException : DomainException
{
    public MissingTenantContextException()
        : base("Missing tenant context")
    {
    }

    public override string Code => "DOMAIN.MISSING_TENANT_CONTEXT";

    public override string I18nKey => "domain.missing_tenant_context";
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message = "Resource not found")
        : base(message, httpStatus: 404)
    {
    }

    public override string Code => "DOMAIN.NOT_FOUND";

    public override string I18nKey => "error.not_found";
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message = "Resource already exists")
        : base(message, httpStatus: 409)
    {
    }

    public override string Code => "DOMAIN.CONFLICT";

    public override string I18nKey => "error.conflict";
}

public sealed class ForbiddenException : DomainException
{
    /// <summary>Stable code shared with the framework 403 document.</summary>
    public const string ErrorCode = "DOMAIN.FORBIDDEN";

    /// <summary>Stable i18n key shared with the framework 403 document.</summary>
    public const string MessageKey = "error.forbidden";

    public ForbiddenException(string message = "Operation not allowed")
        : base(message, httpStatus: 403)
    {
    }

    public override string Code => ErrorCode;

    public override string I18nKey => MessageKey;
}

public sealed class UnauthorizedException : DomainException
{
    /// <summary>Stable code shared with the framework 401 document.</summary>
    public const string ErrorCode = "DOMAIN.UNAUTHORIZED";

    /// <summary>Stable i18n key shared with the framework 401 document.</summary>
    public const string MessageKey = "error.unauthorized";

    public UnauthorizedException(string message = "Authentication required")
        : base(message, httpStatus: 401)
    {
    }

    public override string Code => ErrorCode;

    public override string I18nKey => MessageKey;
}

public sealed class PasswordChangeRequiredException : DomainException
{
    public PasswordChangeRequiredException()
        : base("Password change required before continuing", httpStatus: 403)
    {
    }

    public override string Code => "AUTH.PASSWORD_CHANGE_REQUIRED";

    public override string I18nKey => "auth.password_change_required";
}

public sealed class LegalAcceptanceRequiredException : DomainException
{
    public LegalAcceptanceRequiredException()
        : base("Legal acceptance required before continuing", httpStatus: 403)
    {
    }

    public override string Code => "LEGAL.ACCEPTANCE_REQUIRED";

    public override string I18nKey => "legal.acceptance_required";
}

public sealed class TooManyRequestsException : DomainException
{
    public TooManyRequestsException(string message = "Too many requests")
        : base(message, httpStatus: 429)
    {
    }

    public override string Code => "DOMAIN.TOO_MANY_REQUESTS";

    public override string I18nKey => "error.too_many_requests";
}

/// <summary>The payload is larger than the endpoint accepts.</summary>
public sealed class PayloadTooLargeException : DomainException
{
    public PayloadTooLargeException(string message = "The request payload is too large")
        : base(message, httpStatus: 413)
    {
    }

    public override string Code => "DOMAIN.PAYLOAD_TOO_LARGE";

    public override string I18nKey => "error.payload_too_large";
}

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
