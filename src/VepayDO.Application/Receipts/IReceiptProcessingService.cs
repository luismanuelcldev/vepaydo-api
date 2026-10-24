using VepayDO.Domain.Enums;

namespace VepayDO.Application.Receipts;

public interface IReceiptProcessingService
{
    Task<ReceiptProcessingOutcome> ProcessAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);
}

public enum ReceiptProcessingOutcome
{

    Started,

    AlreadyProcessed,
}
