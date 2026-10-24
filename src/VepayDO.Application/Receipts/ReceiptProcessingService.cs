using VepayDO.Application.Audit;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Application.Storage;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Receipts;

/// <summary>
/// The worker side of the receipt lifecycle (M5.4). It runs inside the tenant
/// transaction of the job, so the RLS policies of the tenant apply and every
/// write is audited with the <c>system</c> actor.
/// </summary>
public sealed class ReceiptProcessingService(
    IReceiptRepository receipts,
    IStorageToolkit storage,
    IAuditWriter audit,
    ITenantContextAccessor tenantContext) : IReceiptProcessingService
{
    public async Task<ReceiptProcessingOutcome> ProcessAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        var receipt = await receipts
            .FindAsync(tenantId.Value, receiptId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Receipt not found");

        if (!string.Equals(receipt.ProcessingStatus, nameof(ProcessingStatus.RECEIVED), StringComparison.Ordinal))
        {
            return ReceiptProcessingOutcome.AlreadyProcessed;
        }

        var stored = await storage.Objects
            .ExistsAsync(receipt.StorageKey, cancellationToken)
            .ConfigureAwait(false);

        if (!stored)
        {
            // The file is gone: the receipt stays in RECEIVED and the job is
            // retried. A missing file is an operational problem, never a reason
            // to lose the receipt.
            throw new InvalidOperationException($"The stored file of receipt {receiptId:D} is missing");
        }

        await receipts
            .MarkProcessingAsync(tenantId.Value, receiptId, cancellationToken)
            .ConfigureAwait(false);

        var draft = new AuditEventBuilder()
            .TenantId(tenantId.Value)
            .Actor(AuditActor.SYSTEM)
            .Entity("receipt")
            .EntityId(receiptId)
            .Action("processing.started")
            .Before(new { processingStatus = receipt.ProcessingStatus })
            .After(new { processingStatus = nameof(ProcessingStatus.PROCESSING) })
            .Build();

        await audit.WriteAsync(draft, cancellationToken).ConfigureAwait(false);

        return ReceiptProcessingOutcome.Started;
    }
}
