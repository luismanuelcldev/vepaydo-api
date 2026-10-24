namespace VepayDO.Domain.Errors;

public sealed class ForbiddenException : DomainException
{
    public const string ErrorCode = "DOMAIN.FORBIDDEN";
    public const string MessageKey = "error.forbidden";

    public ForbiddenException(string message = "Operation not allowed")
        : base(message, httpStatus: 403)
    {
    }

    public override string Code => ErrorCode;

    public override string I18nKey => MessageKey;
}
