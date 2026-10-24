using VepayDO.Application.Receipts;
using VepayDO.Contracts.Receipts;

namespace VepayDO.Api.Controllers.Receipts;

/// <summary>
/// Maps the receipt application results onto the wire contracts, keeping
/// <c>VepayDO.Contracts</c> free of any Application dependency (AGENTS.md section 6).
/// </summary>
internal static class ReceiptMappers
{
    public static ReceiptResponse ToResponse(this ReceiptView receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new ReceiptResponse(
            receipt.Id,
            receipt.Source,
            receipt.ProcessingStatus,
            receipt.ReconciliationStatus,
            receipt.Risk,
            receipt.Mime,
            receipt.Bytes,
            receipt.Width,
            receipt.Height,
            receipt.Amount,
            receipt.Currency,
            receipt.PaymentDate,
            receipt.Reference,
            receipt.SenderName,
            receipt.DestinationLast4,
            receipt.CustomerId,
            receipt.SuggestedChargeId,
            receipt.MatchScore,
            receipt.FailureReason,
            receipt.CreatedAt,
            receipt.UpdatedAt);
    }

    public static IReadOnlyList<ReceiptResponse> ToResponse(this IReadOnlyList<ReceiptView> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);

        return receipts.Select(ToResponse).ToList();
    }
}
