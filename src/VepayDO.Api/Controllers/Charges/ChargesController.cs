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

/// <summary>
/// Charge endpoints under <c>/api/v1/charges</c> (UC-DATA-03 to 06). Read and mutate
/// allow OWNER and VERIFIER (<c>charges.manage</c>, AGENTS.md section 9.5).
/// </summary>
[ApiController]
[Route("charges")]
public sealed class ChargesController(IChargeService charges) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<IReadOnlyList<ChargeResponse>>> List(
        [FromQuery] ChargeListRequest query,
        CancellationToken cancellationToken)
    {
        var result = await charges
            .ListAsync(new ChargeQuery(query.Status, query.CustomerId), cancellationToken)
            .ConfigureAwait(false);

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
    public async Task<ActionResult<ChargeResponse>> Create(
        [FromBody] CreateChargeRequest body,
        CancellationToken cancellationToken)
    {
        var input = new CreateChargeInput(
            body.CustomerId,
            body.Code,
            body.Description,
            body.Amount,
            body.Currency,
            body.IssuedOn,
            body.DueOn);

        var charge = await charges
            .CreateAsync(input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, charge.ToResponse());
    }

    [HttpPatch("{chargeId:guid}")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Update(
        Guid chargeId,
        [FromBody] UpdateChargeRequest body,
        CancellationToken cancellationToken)
    {
        var input = new UpdateChargeInput(
            body.CustomerId,
            body.Code,
            body.Description,
            body.Amount,
            body.Currency,
            body.IssuedOn,
            body.DueOn);

        var charge = await charges
            .UpdateAsync(chargeId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(charge.ToResponse());
    }

    [HttpPost("{chargeId:guid}/void")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Void(
        Guid chargeId,
        [FromBody] VoidChargeRequest body,
        CancellationToken cancellationToken)
    {
        var input = new VoidChargeInput(body.Reason);

        var charge = await charges
            .VoidAsync(chargeId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(charge.ToResponse());
    }

    [HttpPost("{chargeId:guid}/duplicate")]
    [RequirePermission(Permissions.ChargesManage)]
    public async Task<ActionResult<ChargeResponse>> Duplicate(
        Guid chargeId,
        [FromBody] DuplicateChargeRequest body,
        CancellationToken cancellationToken)
    {
        var input = new DuplicateChargeInput(body.Code);

        var charge = await charges
            .DuplicateAsync(chargeId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, charge.ToResponse());
    }

    [HttpPost("import")]
    [RequirePermission(Permissions.ChargesManage)]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<ChargeImportResponse>> Import(
        [FromForm] IFormFile file,
        [FromQuery] bool allOrNothing,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Empty file",
                detail: "A non-empty CSV file is required");
        }

        IReadOnlyList<ChargeImportRow> rows;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            rows = ChargeCsvParser.Parse(content);
        }
        catch (CsvFormatException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Invalid CSV",
                detail: $"Row {ex.RowNumber}: {ex.Message}");
        }

        var result = await charges
            .ImportAsync(rows, allOrNothing, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(result.ToResponse());
    }
}
