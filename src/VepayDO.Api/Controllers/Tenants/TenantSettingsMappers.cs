using VepayDO.Application.Tenants;
using VepayDO.Contracts.Tenants;

namespace VepayDO.Api.Controllers.Tenants;

/// <summary>
/// Maps the tenant settings application results onto the wire contracts, keeping
/// <c>VepayDO.Contracts</c> free of any Application dependency (AGENTS.md section 6).
/// </summary>
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