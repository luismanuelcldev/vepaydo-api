using VepayDO.Domain.Common;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tenants;

/// <summary>
/// The per-tenant configuration values (AGENTS.md section 9.6) as a domain value
/// object. This is the Prototype of the settings subsystem: the canonical
/// defaults live in <see cref="Default"/> and every new tenant's settings are a
/// deep-independent <see cref="Clone"/> of that prototype, so mutating one
/// tenant's values can never leak into the prototype or into another tenant.
/// </summary>
/// <remarks>
/// The prototype carries only <see cref="decimal"/> and integral fields, but the
/// clone is written field-by-field on purpose: if the shape ever grows a
/// reference type, the copy stays deep and the independence test keeps passing.
/// </remarks>
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

    /// <summary>
    /// The canonical, never-mutated prototype of the defaults (section 9.6):
    /// 7-day window, zero tolerance, no verifier cap, 24-month retention.
    /// </summary>
    public static readonly TenantSettingsValues Default = new()
    {
        DateWindowDays = 7,
        AmountTolerance = 0m,
        VerifierAmountCap = null,
        ImageRetentionMonths = 24,
    };

    /// <summary>
    /// Returns a deep, independent copy of this instance (Prototype pattern,
    /// AGENTS.md section 8.4). Implemented explicitly because a positional-free
    /// record already synthesizes an instance <c>Clone()</c> for its copy
    /// constructor; this override routes that contract to an explicit
    /// field-by-field copy so the independence guarantee is self-evident.
    /// </summary>
    TenantSettingsValues ICloneable<TenantSettingsValues>.Clone() => FromPrototype();

    /// <summary>
    /// Builds a fresh, mutable value object from this one, copying every field
    /// explicitly. Callers use it on <see cref="Default"/> to obtain an
    /// independent settings instance per tenant.
    /// </summary>
    public TenantSettingsValues FromPrototype() => new()
    {
        DateWindowDays = DateWindowDays,
        AmountTolerance = AmountTolerance,
        VerifierAmountCap = VerifierAmountCap,
        ImageRetentionMonths = ImageRetentionMonths,
    };

    /// <summary>
    /// Enforces the invariants of section 9.6 shared by every write path, so the
    /// API layer and any future caller reject the same payloads.
    /// </summary>
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