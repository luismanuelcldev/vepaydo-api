namespace VepayDO.Domain.Errors;

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
