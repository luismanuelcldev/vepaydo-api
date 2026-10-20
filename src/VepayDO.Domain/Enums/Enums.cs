namespace VepayDO.Domain.Enums;

// Enum member names intentionally use UPPER_SNAKE_CASE so that ToString(), JSON,
// and the PostgreSQL enum values share one representation (see AGENTS.md section 4).
// CA1707 (underscores in identifiers) is disabled in .editorconfig for this reason.

public enum MemberRole
{
    OWNER,
    VERIFIER,
}

public enum ProcessingStatus
{
    RECEIVED,
    PROCESSING,
    EXTRACTED,
    FAILED,
}

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

public enum AlertSeverity
{
    INFO,
    WARNING,
    CRITICAL,
}

public enum AlertStatus
{
    OPEN,
    RESOLVED,
}

public enum AlertResolution
{
    DISMISSED,
    CONFIRMED_ANYWAY,
    REJECTED,
    AUTO_CLEARED,
}

public enum ChargeStatus
{
    OPEN,
    PARTIAL,
    PAID,
    VOIDED,
}

public enum ReceiptSource
{
    WEB_UPLOAD,
    CAMERA,
    SHARE_TARGET,
}

public enum TransferType
{
    SAME_BANK,
    INTERBANK,
    UNKNOWN,
}

public enum TenantPlan
{
    PILOT,
    ESSENTIAL,
    BUSINESS,
}

public enum AlertRuleCode
{
    DUPLICATE_REFERENCE,
    IDENTICAL_IMAGE,
    SIMILAR_IMAGE,
    FOREIGN_DESTINATION_ACCOUNT,
    FUTURE_DATE,
    DATE_BEFORE_CHARGE,
    STALE_DATE,
    INVALID_REFERENCE_FORMAT,
    AMOUNT_MISMATCH,
    SENDER_MISMATCH,
    LOW_CONFIDENCE,
}

public enum FailureReason
{
    NOT_A_RECEIPT,
    UNREADABLE,
    OCR_ERROR,
    TIMEOUT,
}

public enum RejectReasonCode
{
    WRONG_AMOUNT,
    DUPLICATE,
    NOT_THIS_BUSINESS,
    NOT_A_RECEIPT,
    UNREADABLE,
    OTHER,
}

public enum LegalDocumentType
{
    TERMS,
    PRIVACY,
}

public enum AuditActor
{
    USER,
    SYSTEM,
    SUPPORT,
}
