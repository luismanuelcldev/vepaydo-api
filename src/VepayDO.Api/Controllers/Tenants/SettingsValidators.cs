using FluentValidation;
using VepayDO.Contracts.Tenants;
using VepayDO.Domain.Tenants;

namespace VepayDO.Api.Controllers.Tenants;

/// <summary>
/// Validates the partial settings update (UC-SET-04). The same ranges are also
/// enforced by <see cref="TenantSettingsValues.Validate"/> in the domain, so the
/// rule has one source of truth; this layer only reports them as field errors.
/// </summary>
public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(request => request.DateWindowDays)
            .InclusiveBetween(TenantSettingsValues.MinDateWindowDays, TenantSettingsValues.MaxDateWindowDays)
            .When(request => request.DateWindowDays.HasValue);

        RuleFor(request => request.AmountTolerance)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.AmountTolerance.HasValue);

        RuleFor(request => request.VerifierAmountCap)
            .GreaterThan(0m)
            .When(request => request.VerifierAmountCap.HasValue);

        RuleFor(request => request.ImageRetentionMonths)
            .InclusiveBetween(TenantSettingsValues.MinImageRetentionMonths, TenantSettingsValues.MaxImageRetentionMonths)
            .When(request => request.ImageRetentionMonths.HasValue);
    }
}