namespace VepayDO.Domain.Errors;

public sealed class PasswordChangeRequiredException : DomainException
{
    public PasswordChangeRequiredException()
        : base("Password change required before continuing", httpStatus: 403)
    {
    }

    public override string Code => "AUTH.PASSWORD_CHANGE_REQUIRED";

    public override string I18nKey => "auth.password_change_required";
}
