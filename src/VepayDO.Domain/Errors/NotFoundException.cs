namespace VepayDO.Domain.Errors;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message = "Resource not found")
        : base(message, httpStatus: 404)
    {
    }

    public override string Code => "DOMAIN.NOT_FOUND";

    public override string I18nKey => "error.not_found";
}
