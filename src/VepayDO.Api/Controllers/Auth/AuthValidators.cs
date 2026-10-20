using FluentValidation;

namespace VepayDO.Api.Controllers.Auth;

/// <summary>
/// Validators mirroring the shared Zod schemas (<c>packages/shared/src/schemas.ts</c>)
/// so both APIs reject the same payloads: minimum password length of 12, a valid
/// email address, and the two supported locales (AGENTS.md section 10).
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12);
        RuleFor(request => request.TenantName).NotEmpty().MaximumLength(120);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12);
    }
}

public sealed class SelectTenantRequestValidator : AbstractValidator<SelectTenantRequest>
{
    public SelectTenantRequestValidator()
    {
        RuleFor(request => request.TenantId).NotEmpty();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MinimumLength(12);
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12);
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.PreferredLocale)
            .Must(locale => locale is null or "es-DO" or "en")
            .WithMessage("Preferred locale must be one of es-DO, en.");
    }
}
