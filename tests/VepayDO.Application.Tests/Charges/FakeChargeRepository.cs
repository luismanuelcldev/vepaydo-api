using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Charges;

internal sealed partial class FakeChargeRepository : IChargeRepository
{
    private readonly List<ChargeRecord> _items = [];
    public List<NewCharge> Added { get; } = [];
    public int ConflictsBeforeSuccess { get; set; }

    public void Seed(ChargeRecord record) => _items.Add(record);

    public Task<IReadOnlyList<ChargeRecord>> ListAsync(
        Guid tenantId,
        ChargeQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChargeRecord>>(_items);

    public Task<ChargeRecord?> FindAsync(
        Guid tenantId,
        Guid chargeId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(c => c.Id == chargeId));

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludingChargeId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Any(c => c.Code == code && c.Id != excludingChargeId));

    public Task<Guid> AddAsync(
        Guid tenantId,
        NewCharge charge,
        CancellationToken cancellationToken = default)
    {
        if (ConflictsBeforeSuccess > 0)
        {
            ConflictsBeforeSuccess--;
            return Task.FromException<Guid>(new ConflictException("Charge code already exists in this tenant"));
        }

        var id = Guid.NewGuid();
        Added.Add(charge);
        _items.Add(new ChargeRecord(
            id, charge.CustomerId, charge.Code, charge.Description, charge.Amount, charge.Currency,
            charge.IssuedOn, charge.DueOn, "OPEN", 0m, charge.Amount, null, null,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        return Task.FromResult(id);
    }
}
