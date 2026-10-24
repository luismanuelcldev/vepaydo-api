namespace VepayDO.Application.Charges;

/// <summary>
/// A charge as returned by the API (UC-DATA-03). <see cref="Amount"/> and
/// <see cref="Balance"/> are decimals the client renders but never computes with
/// (AGENTS.md section 5).
/// </summary>
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

/// <summary>A charge row plus its applied/balance aggregation (view-backed).</summary>
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

/// <summary>Create payload for <c>POST /charges</c> (OWNER or VERIFIER, UC-DATA-03).</summary>
public sealed record CreateChargeInput(
    Guid? CustomerId,
    string? Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);

/// <summary>Update payload for <c>PATCH /charges/{id}</c>; all fields optional.</summary>
public sealed record UpdateChargeInput(
    Guid? CustomerId,
    string? Code,
    string? Description,
    decimal? Amount,
    string? Currency,
    DateOnly? IssuedOn,
    DateOnly? DueOn);

/// <summary>Payload for <c>POST /charges/{id}/void</c> (OWNER or VERIFIER).</summary>
public sealed record VoidChargeInput(string Reason);

/// <summary>Payload for <c>POST /charges/{id}/duplicate</c> (UC-DATA-04).</summary>
public sealed record DuplicateChargeInput(string? Code);

/// <summary>A single CSV row error, reported without aborting the import.</summary>
public sealed record ChargeImportRowError(int RowNumber, string Message);

/// <summary>Result of a CSV import (UC-DATA-06).</summary>
public sealed record ChargeImportResult(
    int Imported,
    int Skipped,
    bool AllOrNothing,
    IReadOnlyList<ChargeImportRowError> Errors);

/// <summary>One parsed import row ready to persist.</summary>
public sealed record ChargeImportRow(
    int RowNumber,
    Guid? CustomerId,
    string? Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);
