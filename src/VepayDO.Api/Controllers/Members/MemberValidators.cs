using FluentValidation;
using VepayDO.Contracts.Members;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Controllers.Members;

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
