using FluentValidation;
using VepayDO.Contracts.BusinessAccounts;
using VepayDO.Domain.Billing;

namespace VepayDO.Api.Controllers.BusinessAccounts;

/// <summary>
/// Validates the business-account payloads (UC-SET-03): a known bank, a non-empty
/// label and exactly four digits for the account. The currency must be one of the
/// supported values; the service re-validates domain invariants.
/// </summary>
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

/// <summary>Validates the partial business-account update (UC-SET-03).</summary>
public sealed class UpdateBusinessAccountRequestValidator : AbstractValidator<UpdateBusinessAccountRequest>
{
    public UpdateBusinessAccountRequestValidator()
    {
        RuleFor(request => request.Label).MaximumLength(120).When(request => request.Label is not null);
    }
}
