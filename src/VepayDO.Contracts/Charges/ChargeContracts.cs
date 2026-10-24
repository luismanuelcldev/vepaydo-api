namespace VepayDO.Contracts.Charges;

/// <summary>
/// Wire contracts for the charges module (UC-DATA-03 to 06). Amounts and balances
/// are decimals the client renders but never computes with (AGENTS.md section 5).
/// </summary>
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

/// <summary>Query of <c>GET /charges</c>; both filters are optional.</summary>
public sealed record ChargeListRequest(string? Status, Guid? CustomerId);

/// <summary>Body of <c>POST /charges</c> (OWNER or VERIFIER, UC-DATA-03).</summary>
public sealed record CreateChargeRequest(
    Guid? CustomerId,
    string? Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);

/// <summary>Body of <c>PATCH /charges/{id}</c>; all fields are optional (UC-DATA-03).</summary>
public sealed record UpdateChargeRequest(
    Guid? CustomerId,
    string? Code,
    string? Description,
    decimal? Amount,
    string? Currency,
    DateOnly? IssuedOn,
    DateOnly? DueOn);

/// <summary>Body of <c>POST /charges/{id}/void</c> (UC-DATA-03).</summary>
public sealed record VoidChargeRequest(string Reason);

/// <summary>Body of <c>POST /charges/{id}/duplicate</c> (UC-DATA-04).</summary>
public sealed record DuplicateChargeRequest(string? Code);

/// <summary>A single CSV row error in the import report (UC-DATA-06).</summary>
public sealed record ChargeImportRowErrorResponse(int RowNumber, string Message);

/// <summary>Response of <c>POST /charges/import</c> (UC-DATA-06).</summary>
public sealed record ChargeImportResponse(
    int Imported,
    int Skipped,
    bool AllOrNothing,
    IReadOnlyList<ChargeImportRowErrorResponse> Errors);
