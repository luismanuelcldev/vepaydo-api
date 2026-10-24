namespace VepayDO.Application.Customers;

public sealed record CustomerView(
    Guid Id,
    string Name,
    string NameNormalized,
    string? Phone,
    string? Notes,
    bool IsActive,
    IReadOnlyList<string> Aliases,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerRecord(
    Guid Id,
    string Name,
    string NameNormalized,
    string? Phone,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerAliasRecord(Guid Id, Guid CustomerId, string AliasNormalized);

public sealed record CreateCustomerInput(string Name, string? Phone, string? Notes);

public sealed record UpdateCustomerInput(string? Name, string? Phone, string? Notes, bool? IsActive);

public sealed record AddAliasInput(string Alias);

public sealed record MoveAliasInput(Guid FromCustomerId, string Alias);
