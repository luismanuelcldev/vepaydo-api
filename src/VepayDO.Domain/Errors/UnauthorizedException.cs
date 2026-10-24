namespace VepayDO.Domain.Errors;

public sealed class UnauthorizedException : DomainException
{
    public const string ErrorCode = "DOMAIN.UNAUTHORIZED";
    public const string MessageKey = "error.unauthorized";

    public UnauthorizedException(string message = "Authentication required")
        : base(message, httpStatus: 401)
    {
    }

    public override string Code => ErrorCode;

    public override string I18nKey => MessageKey;
}
