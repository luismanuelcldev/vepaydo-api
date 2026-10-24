using VepayDO.Application.Tenants;
using VepayDO.Contracts.Tenants;

namespace VepayDO.Api.Controllers.Tenants;

internal static class TenantSettingsMappers
{
    public static TenantSettingsResponse ToResponse(this TenantSettingsView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new TenantSettingsResponse(
            view.DateWindowDays,
            view.AmountTolerance,
            view.VerifierAmountCap,
            view.ImageRetentionMonths);
    }
}
