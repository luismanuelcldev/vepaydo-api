namespace VepayDO.Domain.Errors;

public sealed class MissingTenantContextException : DomainException
{
    public MissingTenantContextException()
        : base("Missing tenant context")
    {
    }

    public override string Code => "DOMAIN.MISSING_TENANT_CONTEXT";

    public override string I18nKey => "domain.missing_tenant_context";
}
