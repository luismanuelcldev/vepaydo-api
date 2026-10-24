using VepayDO.Domain.Enums;

namespace VepayDO.Application.Receipts;

/// <summary>
/// Picks up an uploaded receipt and moves it through the processing axis
/// (UC-RCP-04, M5.4).
///
/// <para>
/// The extraction engine is undecided (OCR is deferred), so the worker does
/// what it can without one: it checks that the stored file is really there and
/// moves the receipt to <c>PROCESSING</c>, where the human review of M5.5
/// continues. The receipt never disappears: a failure leaves it in
/// <c>RECEIVED</c> and the job is retried.
/// </para>
/// </summary>
public interface IReceiptProcessingService
{
    Task<ReceiptProcessingOutcome> ProcessAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);
}

/// <summary>What the worker did with a receipt.</summary>
public enum ReceiptProcessingOutcome
{
    /// <summary>The receipt was moved to <c>PROCESSING</c>.</summary>
    Started,

    /// <summary>The receipt was already processed; nothing to do.</summary>
    AlreadyProcessed,
}
