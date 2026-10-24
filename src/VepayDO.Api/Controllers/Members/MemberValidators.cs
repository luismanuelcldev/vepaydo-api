using FluentValidation;
using VepayDO.Contracts.Members;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Controllers.Members;

/// <summary>
/// Validates the member payloads (UC-SET-03): a valid unique email, a non-empty
/// name, and a known role.
/// </summary>
public sealed class CreateMemberRequestValidator : AbstractValidator<CreateMemberRequest>
{
    public CreateMemberRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Role)
            .Must(role => role is MemberRole.OWNER or MemberRole.VERIFIER)
            .WithMessage("Role must be one of OWNER, VERIFIER.");
    }
}

/// <summary>
/// Validates the role/active change (UC-SET-03). Both fields are optional, but a
/// supplied role must be known.
/// </summary>
public sealed class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(request => request.Role)
            .Must(role => role is MemberRole.OWNER or MemberRole.VERIFIER)
            .When(request => request.Role.HasValue)
            .WithMessage("Role must be one of OWNER, VERIFIER.");
    }
}