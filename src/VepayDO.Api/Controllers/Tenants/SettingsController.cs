using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Security;
using VepayDO.Application.Tenants;
using VepayDO.Contracts.Tenants;

namespace VepayDO.Api.Controllers.Tenants;

/// <summary>
/// Tenant settings endpoints under <c>/api/v1/settings</c> (UC-SET-04). Any active
/// member may read the settings; only the OWNER may change them.
/// </summary>
[ApiController]
[Route("settings")]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "MVC actions must be instance members so model binding, filters and base members resolve.")]
public sealed class SettingsController(ITenantSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TenantSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var view = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        return Ok(view.ToResponse());
    }

    [RequirePermission(Permissions.SettingsManage)]
    [HttpPatch]
    public async Task<ActionResult<TenantSettingsResponse>> Update(
        [FromBody] UpdateSettingsRequest body,
        CancellationToken cancellationToken)
    {
        var input = new UpdateTenantSettingsInput(
            body.DateWindowDays,
            body.AmountTolerance,
            body.VerifierAmountCap,
            body.ClearVerifierAmountCap ?? false,
            body.ImageRetentionMonths);

        var view = await settings
            .UpdateAsync(input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(view.ToResponse());
    }
}