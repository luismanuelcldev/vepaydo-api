using FluentValidation;
using VepayDO.Contracts.BusinessAccounts;
using VepayDO.Domain.Billing;

namespace VepayDO.Api.Controllers.BusinessAccounts;

public sealed class CreateBusinessAccountRequestValidator : AbstractValidator<CreateBusinessAccountRequest>
{
    public CreateBusinessAccountRequestValidator()
    {
        RuleFor(request => request.BankId).GreaterThan((short)0);
        RuleFor(request => request.Label).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Last4)
            .NotEmpty()
            .Matches(@"^[0-9]{4}$")
            .WithMessage("Last4 must be exactly four digits.");
        RuleFor(request => request.Currency)
            .Must(currency => Currencies.IsSupported(currency?.Trim().ToUpperInvariant()))
            .WithMessage("Currency must be one of DOP, USD.");
    }
}

public sealed class UpdateBusinessAccountRequestValidator : AbstractValidator<UpdateBusinessAccountRequest>
{
    public UpdateBusinessAccountRequestValidator()
    {
        RuleFor(request => request.Label).MaximumLength(120).When(request => request.Label is not null);
    }
}
