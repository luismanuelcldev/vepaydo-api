using FluentValidation;

using VepayDO.Contracts.Charges;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Charges;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Controllers.Charges;

public sealed class ChargeListRequestValidator : AbstractValidator<ChargeListRequest>
{
    public ChargeListRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(BeAKnownStatus)
            .When(request => !string.IsNullOrWhiteSpace(request.Status))
            .WithMessage("Status is not a known charge status");
    }

    private static bool BeAKnownStatus(string? status) =>
        Enum.TryParse<ChargeStatus>(status, ignoreCase: true, out _);
}

public sealed class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequest>
{
    public CreateChargeRequestValidator()
    {
        RuleFor(request => request.Description).NotEmpty().MaximumLength(500);
        RuleFor(request => request.Amount).GreaterThan(0m);
        RuleFor(request => request.Currency)
            .NotEmpty()
            .Must(Currencies.IsSupported)
            .WithMessage("Currency must be one of the supported values");
        RuleFor(request => request.Code)
            .Must(BeAValidCode)
            .When(request => !string.IsNullOrWhiteSpace(request.Code))
            .WithMessage("Charge code has an invalid format");
        RuleFor(request => request.DueOn)
            .GreaterThanOrEqualTo(request => request.IssuedOn)
            .When(request => request.DueOn is not null)
            .WithMessage("Due date cannot be before the issue date");
    }

    private static bool BeAValidCode(string? code)
    {
        try
        {
            ChargeCode.Parse(code);
            return true;
        }
        catch (VepayDO.Domain.Errors.DomainException)
        {
            return false;
        }
    }
}

public sealed class UpdateChargeRequestValidator : AbstractValidator<UpdateChargeRequest>
{
    public UpdateChargeRequestValidator()
    {
        RuleFor(request => request.Description)
            .NotEmpty()
            .MaximumLength(500)
            .When(request => request.Description is not null);
        RuleFor(request => request.Amount)
            .GreaterThan(0m)
            .When(request => request.Amount is not null);
        RuleFor(request => request.Currency)
            .Must(Currencies.IsSupported)
            .When(request => request.Currency is not null)
            .WithMessage("Currency must be one of the supported values");
        RuleFor(request => request.Code)
            .Must(BeAValidCode)
            .When(request => !string.IsNullOrWhiteSpace(request.Code))
            .WithMessage("Charge code has an invalid format");
    }

    private static bool BeAValidCode(string? code)
    {
        try
        {
            ChargeCode.Parse(code);
            return true;
        }
        catch (VepayDO.Domain.Errors.DomainException)
        {
            return false;
        }
    }
}

public sealed class VoidChargeRequestValidator : AbstractValidator<VoidChargeRequest>
{
    public VoidChargeRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}
