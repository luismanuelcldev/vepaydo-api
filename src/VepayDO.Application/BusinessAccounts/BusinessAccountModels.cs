namespace VepayDO.Application.BusinessAccounts;

/// <summary>
/// A business bank account as returned by the API (UC-SET-03). The full account
/// number never leaves the database: only <see cref="Last4"/> is carried (AGENTS.md
/// section 7).
/// </summary>
public sealed record BusinessAccountView(
    Guid Id,
    short BankId,
    string BankName,
    string Label,
    string Last4,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A row read from <c>business_accounts</c> joined with its bank name.</summary>
public sealed record BusinessAccountRecord(
    Guid Id,
    short BankId,
    string BankName,
    string Label,
    string Last4,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>New account payload for the repository layer.</summary>
public sealed record NewBusinessAccount(
    short BankId,
    string Label,
    string Last4,
    string Currency,
    bool IsActive);

/// <summary>Create payload for <c>POST /business-accounts</c> (OWNER only).</summary>
public sealed record CreateBusinessAccountInput(
    short BankId,
    string Label,
    string Last4,
    string Currency);

/// <summary>Update payload for <c>PATCH /business-accounts/{id}</c>; both fields optional.</summary>
public sealed record UpdateBusinessAccountInput(string? Label, bool? IsActive);
