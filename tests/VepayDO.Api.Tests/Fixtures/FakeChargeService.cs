using VepayDO.Application.Auth;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeChargeService : IChargeService
{
    public IReadOnlyList<ChargeView> Charges { get; set; } = [];

    public Exception? Error { get; set; }

    public IReadOnlyList<ChargeImportRow>? LastImport { get; private set; }

    public bool LastImportAllOrNothing { get; private set; }

    public void Reset()
    {
        Charges = [];
        Error = null;
        LastImport = null;
        LastImportAllOrNothing = false;
    }

    public Task<IReadOnlyList<ChargeView>> ListAsync(
        ChargeQuery query,
        CancellationToken cancellationToken = default) =>
        Error is not null
            ? Task.FromException<IReadOnlyList<ChargeView>>(Error)
            : Task.FromResult(Charges);

    public Task<ChargeView> GetAsync(Guid chargeId, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            return Task.FromException<ChargeView>(Error);
        }

        var found = Charges.FirstOrDefault(c => c.Id == chargeId);
        return found is null
            ? Task.FromException<ChargeView>(new VepayDO.Domain.Errors.NotFoundException("Charge not found"))
            : Task.FromResult(found);
    }

    public Task<ChargeView> CreateAsync(
        CreateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<ChargeView>(Error);
        }

        return Task.FromResult(Build(Guid.NewGuid(), input.Description, input.Amount, input.Currency, "OPEN"));
    }

    public Task<ChargeView> UpdateAsync(
        Guid chargeId,
        UpdateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<ChargeView>(Error);
        }

        return Task.FromResult(Build(
            chargeId,
            input.Description ?? "Charge",
            input.Amount ?? 0m,
            input.Currency ?? "DOP",
            "OPEN"));
    }
}
