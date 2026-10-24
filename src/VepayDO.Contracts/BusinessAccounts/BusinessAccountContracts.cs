namespace VepayDO.Contracts.BusinessAccounts;

/// <summary>
/// Wire contracts for the business-accounts module (UC-SET-03). Only the last
/// four digits are ever carried; the full account number never leaves the server
/// (AGENTS.md section 7).
/// </summary>
public sealed record BusinessAccountResponse(
    Guid Id,
    short BankId,
    string BankName,
    string Label,
    string Last4,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Body of <c>POST /business-accounts</c> (OWNER only).</summary>
public sealed record CreateBusinessAccountRequest(short BankId, string Label, string Last4, string Currency);

/// <summary>Body of <c>PATCH /business-accounts/{id}</c>; both fields are optional.</summary>
public sealed record UpdateBusinessAccountRequest(string? Label, bool? IsActive);
