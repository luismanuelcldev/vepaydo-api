using FluentValidation;

namespace VepayDO.Api.Tests.Probe;

/// <summary>
/// Proves the kernel validation pipeline: a validator registered for an action
/// argument turns into a 422 problem-details response before the action runs.
/// </summary>
public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("name is required");
    }
}
