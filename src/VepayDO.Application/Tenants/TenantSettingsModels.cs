using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tenants;

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

public sealed record UpdateTenantSettingsInput(
    short? DateWindowDays,
    decimal? AmountTolerance,
    decimal? VerifierAmountCap,
    bool ClearVerifierAmountCap,
    short? ImageRetentionMonths)
{

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
