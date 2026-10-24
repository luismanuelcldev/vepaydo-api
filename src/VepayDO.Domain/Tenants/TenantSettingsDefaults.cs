using VepayDO.Domain.Common;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tenants;

public sealed record TenantSettingsValues : ICloneable<TenantSettingsValues>
{
    public const short MinDateWindowDays = 1;
    public const short MaxDateWindowDays = 60;
    public const short MinImageRetentionMonths = 1;
    public const short MaxImageRetentionMonths = 120;

    public required short DateWindowDays { get; init; }

    public required decimal AmountTolerance { get; init; }

    public decimal? VerifierAmountCap { get; init; }

    public required short ImageRetentionMonths { get; init; }

    public static readonly TenantSettingsValues Default = new()
    {
        DateWindowDays = 7,
        AmountTolerance = 0m,
        VerifierAmountCap = null,
        ImageRetentionMonths = 24,
    };

    TenantSettingsValues ICloneable<TenantSettingsValues>.Clone() => FromPrototype();

    public TenantSettingsValues FromPrototype() => new()
    {
        DateWindowDays = DateWindowDays,
        AmountTolerance = AmountTolerance,
        VerifierAmountCap = VerifierAmountCap,
        ImageRetentionMonths = ImageRetentionMonths,
    };

    public void Validate()
    {
        if (DateWindowDays is < MinDateWindowDays or > MaxDateWindowDays)
        {
            throw new InvalidArgumentDomainException(
                $"Date window must be between {MinDateWindowDays} and {MaxDateWindowDays} days");
        }

        if (AmountTolerance < 0m)
        {
            throw new InvalidArgumentDomainException("Amount tolerance cannot be negative");
        }

        if (VerifierAmountCap is { } cap && cap <= 0m)
        {
            throw new InvalidArgumentDomainException("Verifier amount cap must be greater than zero or absent");
        }

        if (ImageRetentionMonths is < MinImageRetentionMonths or > MaxImageRetentionMonths)
        {
            throw new InvalidArgumentDomainException(
                $"Image retention must be between {MinImageRetentionMonths} and {MaxImageRetentionMonths} months");
        }
    }
}
