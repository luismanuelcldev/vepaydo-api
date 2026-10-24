namespace VepayDO.Application.Charges;

public sealed record ChargeView(
    Guid Id,
    Guid? CustomerId,
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn,
    string Status,
    decimal AmountApplied,
    decimal Balance,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ChargeRecord(
    Guid Id,
    Guid? CustomerId,
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn,
    string Status,
    decimal AmountApplied,
    decimal Balance,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateChargeInput(
    Guid? CustomerId,
    string? Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);

public sealed record UpdateChargeInput(
    Guid? CustomerId,
    string? Code,
    string? Description,
    decimal? Amount,
    string? Currency,
    DateOnly? IssuedOn,
    DateOnly? DueOn);

public sealed record VoidChargeInput(string Reason);

public sealed record DuplicateChargeInput(string? Code);

public sealed record ChargeImportRowError(int RowNumber, string Message);

public sealed record ChargeImportResult(
    int Imported,
    int Skipped,
    bool AllOrNothing,
    IReadOnlyList<ChargeImportRowError> Errors);

public sealed record ChargeImportRow(
    int RowNumber,
    Guid? CustomerId,
    string? Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);
