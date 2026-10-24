using VepayDO.Application.Charges;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Controllers.Charges;

/// <summary>
/// Maps the charge application results onto the wire contracts, keeping
/// <c>VepayDO.Contracts</c> free of any Application dependency (AGENTS.md section 6).
/// </summary>
internal static class ChargeMappers
{
    public static ChargeResponse ToResponse(this ChargeView charge)
    {
        ArgumentNullException.ThrowIfNull(charge);

        return new ChargeResponse(
            charge.Id,
            charge.CustomerId,
            charge.Code,
            charge.Description,
            charge.Amount,
            charge.Currency,
            charge.IssuedOn,
            charge.DueOn,
            charge.Status,
            charge.AmountApplied,
            charge.Balance,
            charge.VoidedAt,
            charge.VoidReason,
            charge.CreatedAt,
            charge.UpdatedAt);
    }

    public static IReadOnlyList<ChargeResponse> ToResponse(this IReadOnlyList<ChargeView> charges)
    {
        ArgumentNullException.ThrowIfNull(charges);

        return charges.Select(ToResponse).ToList();
    }

    public static ChargeImportRowErrorResponse ToResponse(this ChargeImportRowError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ChargeImportRowErrorResponse(error.RowNumber, error.Message);
    }

    public static ChargeImportResponse ToResponse(this ChargeImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ChargeImportResponse(
            result.Imported,
            result.Skipped,
            result.AllOrNothing,
            result.Errors.Select(ToResponse).ToList());
    }
}
