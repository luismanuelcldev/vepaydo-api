namespace VepayDO.Application.Customers;

/// <summary>
/// A customer with its aliases as returned by the API (UC-DATA-01/02). The
/// normalized form is exposed so the client can reason about matching without
/// re-implementing the normalizer.
/// </summary>
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

/// <summary>A customer row without its aliases (aliases are loaded separately).</summary>
public sealed record CustomerRecord(
    Guid Id,
    string Name,
    string NameNormalized,
    string? Phone,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>An alias row with the customer it is attached to.</summary>
public sealed record CustomerAliasRecord(Guid Id, Guid CustomerId, string AliasNormalized);

/// <summary>Create payload for <c>POST /customers</c> (OWNER or VERIFIER).</summary>
public sealed record CreateCustomerInput(string Name, string? Phone, string? Notes);

/// <summary>Update payload for <c>PATCH /customers/{id}</c>; all fields optional.</summary>
public sealed record UpdateCustomerInput(string? Name, string? Phone, string? Notes, bool? IsActive);

/// <summary>Payload for <c>POST /customers/{id}/aliases</c> (UC-DATA-02).</summary>
public sealed record AddAliasInput(string Alias);

/// <summary>
/// Payload for <c>POST /customers/{id}/aliases/move</c> (OWNER only, UC-DATA-02):
/// the alias currently owned by <see cref="FromCustomerId"/> is moved to the
/// customer in the route.
/// </summary>
public sealed record MoveAliasInput(Guid FromCustomerId, string Alias);
