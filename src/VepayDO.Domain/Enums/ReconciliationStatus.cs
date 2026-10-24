namespace VepayDO.Domain.Enums;

public enum ReconciliationStatus
{
    PENDING_REVIEW,
    CONFIRMED,
    PARTIAL,
    OVERPAID,
    DUPLICATE,
    SUSPICIOUS,
    UNMATCHED,
    REJECTED,
}
