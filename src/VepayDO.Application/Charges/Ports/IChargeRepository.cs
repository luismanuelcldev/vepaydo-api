namespace VepayDO.Application.Charges.Ports;

using VepayDO.Application.Charges;

/// <summary>
/// Reads and writes the current tenant's <c>charges</c> rows under the request's
/// tenant context (RLS <c>tenant_isolation</c>). Balances are read from the
/// <c>v_charge_balances</c> view (AGENTS.md sections 7, 9.2). Charge codes are
/// unique per tenant; the repository surfaces a duplicate-code signal so the
/// service can retry generation instead of leaking a raw constraint violation.
/// </summary>
public interface IChargeRepository
{
    /// <summary>Lists the tenant's charges joined with their balances, newest first.</summary>
    Task<IReadOnlyList<ChargeRecord>> ListAsync(
        Guid tenantId,
        ChargeQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one charge (with balance) of the tenant, or <c>null</c>.</summary>
    Task<ChargeRecord?> FindAsync(
        Guid tenantId,
        Guid chargeId,
        CancellationToken cancellationToken = default);

    /// <summary>True when a charge with the same code already exists in the tenant.</summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludingChargeId,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a charge and returns its generated id.</summary>
    Task<Guid> AddAsync(
        Guid tenantId,
        NewCharge charge,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the mutable fields of an open charge.</summary>
    Task UpdateAsync(
        Guid tenantId,
        Guid chargeId,
        UpdateChargeFields fields,
        CancellationToken cancellationToken = default);

    /// <summary>Voids a charge (status <c>VOIDED</c>, stamp and reason recorded).</summary>
    Task VoidAsync(
        Guid tenantId,
        Guid chargeId,
        string reason,
        Guid? voidedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts many charges in one transaction (CSV import, UC-DATA-06).</summary>
    Task AddManyAsync(
        Guid tenantId,
        IReadOnlyList<NewCharge> charges,
        CancellationToken cancellationToken = default);
}

/// <summary>Filters for the charge list; MVP keeps it simple (status + customer).</summary>
public sealed record ChargeQuery(string? Status, Guid? CustomerId);

/// <summary>New charge payload for the repository layer.</summary>
public sealed record NewCharge(
    Guid? CustomerId,
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn,
    Guid? CreatedBy);

/// <summary>Mutable fields applied by <c>PATCH</c>.</summary>
public sealed record UpdateChargeFields(
    Guid? CustomerId,
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);

/// <summary>
/// Random source for <c>ChargeCode.Generate</c>. Abstracted so the generator is
/// deterministic in tests and cryptographically seeded in production.
/// </summary>
public interface IChargeCodeSource
{
    int NextValue(int maxExclusive);
}
