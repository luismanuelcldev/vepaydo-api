using VepayDO.Application.Auth;

namespace VepayDO.Application.Customers;

/// <summary>
/// Customer and alias use cases (UC-DATA-01, UC-DATA-02). Read/mutate is allowed
/// for OWNER and VERIFIER; moving an alias between customers is OWNER-only
/// (enforced by the HTTP route policy). Reads and writes run inside the request
/// transaction.
/// </summary>
public interface ICustomerService
{
    /// <summary>Lists the current tenant's customers with their aliases.</summary>
    Task<IReadOnlyList<CustomerView>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns one customer or throws when it does not belong to the tenant.</summary>
    Task<CustomerView> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Creates a customer (name normalized) and writes one audit event.</summary>
    Task<CustomerView> CreateAsync(
        CreateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the mutable fields and writes one audit event.</summary>
    Task<CustomerView> UpdateAsync(
        Guid customerId,
        UpdateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a sender alias (UC-DATA-02). A conflict with another customer's alias
    /// is surfaced as a controlled domain error; re-adding the customer's own
    /// alias is idempotent.
    /// </summary>
    Task<CustomerView> AddAliasAsync(
        Guid customerId,
        AddAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an alias from another customer to this one (OWNER-only). Writes one
    /// audit event recording both customers.
    /// </summary>
    Task<CustomerView> MoveAliasAsync(
        Guid customerId,
        MoveAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
