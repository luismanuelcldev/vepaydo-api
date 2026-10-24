using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;

namespace VepayDO.Application.Tests.Charges;

public sealed partial class ChargeServiceTests
{
    private sealed partial class FakeChargeRepository
    {
        public Task UpdateAsync(Guid tenantId, Guid chargeId, UpdateChargeFields fields, CancellationToken ct = default)
        {
            var index = _items.FindIndex(c => c.Id == chargeId);
            if (index >= 0)
            {
                _items[index] = _items[index] with
                {
                    CustomerId = fields.CustomerId, Code = fields.Code, Description = fields.Description,
                    Amount = fields.Amount, Currency = fields.Currency, IssuedOn = fields.IssuedOn, DueOn = fields.DueOn,
                    Balance = fields.Amount - _items[index].AmountApplied,
                };
            }
            return Task.CompletedTask;
        }

        public Task VoidAsync(Guid tenantId, Guid chargeId, string reason, Guid? voidedBy, CancellationToken ct = default)
        {
            var index = _items.FindIndex(c => c.Id == chargeId);
            if (index >= 0) _items[index] = _items[index] with { Status = "VOIDED", VoidReason = reason, VoidedAt = DateTimeOffset.UnixEpoch };
            return Task.CompletedTask;
        }

        public Task AddManyAsync(Guid tenantId, IReadOnlyList<NewCharge> charges, CancellationToken ct = default)
        {
            foreach (var charge in charges)
            {
                Added.Add(charge);
                _items.Add(new ChargeRecord(Guid.NewGuid(), charge.CustomerId, charge.Code, charge.Description, charge.Amount, charge.Currency, charge.IssuedOn, charge.DueOn, "OPEN", 0m, charge.Amount, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
            }
            return Task.CompletedTask;
        }
    }
}
