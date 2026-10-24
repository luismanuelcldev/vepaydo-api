namespace VepayDO.Domain.Errors;

public sealed class LegalAcceptanceRequiredException : DomainException
{
    public LegalAcceptanceRequiredException()
        : base("Legal acceptance required before continuing", httpStatus: 403)
    {
    }

    public override string Code => "LEGAL.ACCEPTANCE_REQUIRED";

    public override string I18nKey => "legal.acceptance_required";
}
