namespace VepayDO.Application.BusinessAccounts;

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

public sealed record NewBusinessAccount(
    short BankId,
    string Label,
    string Last4,
    string Currency,
    bool IsActive);

public sealed record CreateBusinessAccountInput(
    short BankId,
    string Label,
    string Last4,
    string Currency);

public sealed record UpdateBusinessAccountInput(string? Label, bool? IsActive);
