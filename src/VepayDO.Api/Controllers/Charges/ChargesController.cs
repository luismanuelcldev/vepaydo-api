using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Security;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Controllers.Charges;

[ApiController]
[Route("charges")]
public sealed partial class ChargesController(IChargeService charges) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<IReadOnlyList<ChargeResponse>>> List([FromQuery] ChargeListRequest query, CancellationToken cancellationToken)
    {
        var result = await charges.ListAsync(new ChargeQuery(query.Status, query.CustomerId), cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }

    [HttpGet("{chargeId:guid}")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Get(Guid chargeId, CancellationToken cancellationToken)
    {
        var charge = await charges.GetAsync(chargeId, cancellationToken).ConfigureAwait(false);
        return Ok(charge.ToResponse());
    }

    [HttpPost]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Create([FromBody] CreateChargeRequest body, CancellationToken cancellationToken)
    {
        var input = new CreateChargeInput(body.CustomerId, body.Code, body.Description, body.Amount, body.Currency, body.IssuedOn, body.DueOn);
        var charge = await charges.CreateAsync(input, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, charge.ToResponse());
    }

    [HttpPatch("{chargeId:guid}")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Update(Guid chargeId, [FromBody] UpdateChargeRequest body, CancellationToken cancellationToken)
    {
        var input = new UpdateChargeInput(body.CustomerId, body.Code, body.Description, body.Amount, body.Currency, body.IssuedOn, body.DueOn);
        var charge = await charges.UpdateAsync(chargeId, input, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return Ok(charge.ToResponse());
    }

    [HttpPost("{chargeId:guid}/void")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Void(Guid chargeId, [FromBody] VoidChargeRequest body, CancellationToken cancellationToken)
    {
        var input = new VoidChargeInput(body.Reason);
        var charge = await charges.VoidAsync(chargeId, input, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return Ok(charge.ToResponse());
    }

    [HttpPost("{chargeId:guid}/duplicate")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Duplicate(Guid chargeId, [FromBody] DuplicateChargeRequest body, CancellationToken cancellationToken)
    {
        var input = new DuplicateChargeInput(body.Code);
        var charge = await charges.DuplicateAsync(chargeId, input, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, charge.ToResponse());
    }
}
