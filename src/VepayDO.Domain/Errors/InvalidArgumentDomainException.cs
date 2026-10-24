namespace VepayDO.Domain.Errors;

public sealed class InvalidArgumentDomainException : DomainException
{
    public InvalidArgumentDomainException(string message)
        : base(message)
    {
    }

    public override string Code => "DOMAIN.INVALID_ARGUMENT";

    public override string I18nKey => "domain.invalid_argument";
}
