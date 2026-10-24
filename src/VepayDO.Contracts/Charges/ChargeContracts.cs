namespace VepayDO.Contracts.Charges;

public sealed record ChargeResponse(
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

public sealed record ChargeListRequest(string? Status, Guid? CustomerId);

public sealed record CreateChargeRequest(
    Guid? CustomerId,
    string? Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);

public sealed record UpdateChargeRequest(
    Guid? CustomerId,
    string? Code,
    string? Description,
    decimal? Amount,
    string? Currency,
    DateOnly? IssuedOn,
    DateOnly? DueOn);

public sealed record VoidChargeRequest(string Reason);

public sealed record DuplicateChargeRequest(string? Code);

public sealed record ChargeImportRowErrorResponse(int RowNumber, string Message);

public sealed record ChargeImportResponse(
    int Imported,
    int Skipped,
    bool AllOrNothing,
    IReadOnlyList<ChargeImportRowErrorResponse> Errors);
