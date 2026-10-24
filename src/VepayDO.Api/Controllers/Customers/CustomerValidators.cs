using FluentValidation;
using VepayDO.Contracts.Customers;
using VepayDO.Domain.Customers;

namespace VepayDO.Api.Controllers.Customers;

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(CustomerName.MaxLength);
        RuleFor(request => request.Phone).MaximumLength(32).When(request => request.Phone is not null);
        RuleFor(request => request.Notes).MaximumLength(500).When(request => request.Notes is not null);
    }
}

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

public sealed class AddAliasRequestValidator : AbstractValidator<AddAliasRequest>
{
    public AddAliasRequestValidator()
    {
        RuleFor(request => request.Alias).NotEmpty().MaximumLength(CustomerName.MaxLength);
    }
}

public sealed class MoveAliasRequestValidator : AbstractValidator<MoveAliasRequest>
{
    public MoveAliasRequestValidator()
    {
        RuleFor(request => request.FromCustomerId).NotEmpty();
        RuleFor(request => request.Alias).NotEmpty().MaximumLength(CustomerName.MaxLength);
    }
}
