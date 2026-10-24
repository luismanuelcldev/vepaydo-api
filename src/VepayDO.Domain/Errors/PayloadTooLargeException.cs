namespace VepayDO.Domain.Errors;

public sealed class PayloadTooLargeException : DomainException
{
    public PayloadTooLargeException(string message = "The request payload is too large")
        : base(message, httpStatus: 413)
    {
    }

    public override string Code => "DOMAIN.PAYLOAD_TOO_LARGE";

    public override string I18nKey => "error.payload_too_large";
}
