namespace VepayDO.Contracts.BusinessAccounts;

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

public sealed record CreateBusinessAccountRequest(short BankId, string Label, string Last4, string Currency);

public sealed record UpdateBusinessAccountRequest(string? Label, bool? IsActive);
