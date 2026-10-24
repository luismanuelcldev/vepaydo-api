using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Charges;
using VepayDO.Application.Security;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Controllers.Charges;

public sealed partial class ChargesController
{
    [HttpPost("import")]
    [RequirePermission(Permissions.ChargesManage)]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<ChargeImportResponse>> Import([FromForm] IFormFile file, [FromQuery] bool allOrNothing, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Empty file", detail: "A non-empty CSV file is required");

        IReadOnlyList<ChargeImportRow> rows;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            rows = ChargeCsvParser.Parse(content);
        }
        catch (CsvFormatException ex)
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Invalid CSV", detail: $"Row {ex.RowNumber}: {ex.Message}");
        }

        var result = await charges.ImportAsync(rows, allOrNothing, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }
}
