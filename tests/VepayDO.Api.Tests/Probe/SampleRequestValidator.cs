using FluentValidation;

namespace VepayDO.Api.Tests.Probe;

public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("name is required");
    }
}
