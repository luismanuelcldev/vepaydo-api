namespace VepayDO.Domain.Errors;

public sealed class ConflictException : DomainException
{
    public ConflictException(string message = "Resource already exists")
        : base(message, httpStatus: 409)
    {
    }

    public override string Code => "DOMAIN.CONFLICT";

    public override string I18nKey => "error.conflict";
}
