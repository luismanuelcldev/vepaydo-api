using FluentValidation;
using VepayDO.Contracts.Customers;
using VepayDO.Domain.Customers;

namespace VepayDO.Api.Controllers.Customers;

/// <summary>
/// Validates the customer payloads (UC-DATA-01): a non-empty name within the
/// domain limit. Phone and notes are free text.
/// </summary>
public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(CustomerName.MaxLength);
        RuleFor(request => request.Phone).MaximumLength(32).When(request => request.Phone is not null);
        RuleFor(request => request.Notes).MaximumLength(500).When(request => request.Notes is not null);
    }
}

/// <summary>Validates the partial customer update (UC-DATA-01).</summary>
public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CustomerName.MaxLength)
            .When(request => request.Name is not null);
        RuleFor(request => request.Phone).MaximumLength(32).When(request => request.Phone is not null);
        RuleFor(request => request.Notes).MaximumLength(500).When(request => request.Notes is not null);
    }
}

/// <summary>Validates an alias payload (UC-DATA-02).</summary>
public sealed class AddAliasRequestValidator : AbstractValidator<AddAliasRequest>
{
    public AddAliasRequestValidator()
    {
        RuleFor(request => request.Alias).NotEmpty().MaximumLength(CustomerName.MaxLength);
    }
}

/// <summary>Validates a move-alias payload (UC-DATA-02).</summary>
public sealed class MoveAliasRequestValidator : AbstractValidator<MoveAliasRequest>
{
    public MoveAliasRequestValidator()
    {
        RuleFor(request => request.FromCustomerId).NotEmpty();
        RuleFor(request => request.Alias).NotEmpty().MaximumLength(CustomerName.MaxLength);
    }
}
