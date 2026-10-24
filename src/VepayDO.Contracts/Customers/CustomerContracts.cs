namespace VepayDO.Contracts.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string NameNormalized,
    string? Phone,
    string? Notes,
    bool IsActive,
    IReadOnlyList<string> Aliases,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateCustomerRequest(string Name, string? Phone, string? Notes);

public sealed record UpdateCustomerRequest(string? Name, string? Phone, string? Notes, bool? IsActive);

public sealed record AddAliasRequest(string Alias);

public sealed record MoveAliasRequest(Guid FromCustomerId, string Alias);
