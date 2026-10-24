namespace VepayDO.Domain.Errors;

public sealed class TooManyRequestsException : DomainException
{
    public TooManyRequestsException(string message = "Too many requests")
        : base(message, httpStatus: 429)
    {
    }

    public override string Code => "DOMAIN.TOO_MANY_REQUESTS";

    public override string I18nKey => "error.too_many_requests";
}
