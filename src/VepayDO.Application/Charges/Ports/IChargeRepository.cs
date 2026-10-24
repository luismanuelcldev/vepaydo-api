namespace VepayDO.Application.Charges.Ports;

using VepayDO.Application.Charges;

public interface IChargeRepository
{

    Task<IReadOnlyList<ChargeRecord>> ListAsync(
        Guid tenantId,
        ChargeQuery query,
        CancellationToken cancellationToken = default);

    Task<ChargeRecord?> FindAsync(
        Guid tenantId,
        Guid chargeId,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludingChargeId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(
        Guid tenantId,
        NewCharge charge,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid tenantId,
        Guid chargeId,
        UpdateChargeFields fields,
        CancellationToken cancellationToken = default);

    Task VoidAsync(
        Guid tenantId,
        Guid chargeId,
        string reason,
        Guid? voidedBy,
        CancellationToken cancellationToken = default);

    Task AddManyAsync(
        Guid tenantId,
        IReadOnlyList<NewCharge> charges,
        CancellationToken cancellationToken = default);
}

public sealed record ChargeQuery(string? Status, Guid? CustomerId);

public sealed record NewCharge(
    Guid? CustomerId,
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn,
    Guid? CreatedBy);

public sealed record UpdateChargeFields(
    Guid? CustomerId,
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly IssuedOn,
    DateOnly? DueOn);

public interface IChargeCodeSource
{
    int NextValue(int maxExclusive);
}
