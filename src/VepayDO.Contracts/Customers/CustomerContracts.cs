namespace VepayDO.Contracts.Customers;

/// <summary>
/// Wire contracts for the customers module (UC-DATA-01, UC-DATA-02). The
/// normalized name is exposed so the client can reason about matching without
/// re-implementing the normalizer.
/// </summary>
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

/// <summary>Body of <c>POST /customers</c> (OWNER or VERIFIER).</summary>
public sealed record CreateCustomerRequest(string Name, string? Phone, string? Notes);

/// <summary>Body of <c>PATCH /customers/{id}</c>; all fields are optional.</summary>
public sealed record UpdateCustomerRequest(string? Name, string? Phone, string? Notes, bool? IsActive);

/// <summary>Body of <c>POST /customers/{id}/aliases</c> (UC-DATA-02).</summary>
public sealed record AddAliasRequest(string Alias);

/// <summary>Body of <c>POST /customers/{id}/aliases/move</c> (OWNER only, UC-DATA-02).</summary>
public sealed record MoveAliasRequest(Guid FromCustomerId, string Alias);
