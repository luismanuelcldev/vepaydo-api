using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tenants;

/// <summary>Per-tenant settings as returned by <c>GET /settings</c> (UC-SET-04).</summary>
public sealed record TenantSettingsView(
    short DateWindowDays,
    decimal AmountTolerance,
    decimal? VerifierAmountCap,
    short ImageRetentionMonths)
{
    public static TenantSettingsView From(TenantSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new TenantSettingsView(
            values.DateWindowDays,
            values.AmountTolerance,
            values.VerifierAmountCap,
            values.ImageRetentionMonths);
    }
}

/// <summary>
/// A partial update of the tenant settings (UC-SET-04). A <c>null</c> field leaves
/// the current value untouched; <see cref="VerifierAmountCap"/> is nullable on
/// purpose so the cap can be cleared, so callers use
/// <see cref="ClearVerifierAmountCap"/> to distinguish "leave as is" from "remove".
/// </summary>
public sealed record UpdateTenantSettingsInput(
    short? DateWindowDays,
    decimal? AmountTolerance,
    decimal? VerifierAmountCap,
    bool ClearVerifierAmountCap,
    short? ImageRetentionMonths)
{
    /// <summary>Merges this partial update onto the given current values.</summary>
    public TenantSettingsValues ApplyTo(TenantSettingsValues current)
    {
        ArgumentNullException.ThrowIfNull(current);

        return new TenantSettingsValues
        {
            DateWindowDays = DateWindowDays ?? current.DateWindowDays,
            AmountTolerance = AmountTolerance ?? current.AmountTolerance,
            VerifierAmountCap = ClearVerifierAmountCap
                ? null
                : VerifierAmountCap ?? current.VerifierAmountCap,
            ImageRetentionMonths = ImageRetentionMonths ?? current.ImageRetentionMonths,
        };
    }
}