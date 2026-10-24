using VepayDO.Application.Auth;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeChargeService
{

    public Task<ChargeView> VoidAsync(
        Guid chargeId,
        VoidChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<ChargeView>(Error);
        }

        return Task.FromResult(Build(chargeId, "Charge", 0m, "DOP", "VOIDED"));
    }

    public Task<ChargeView> DuplicateAsync(
        Guid chargeId,
        DuplicateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<ChargeView>(Error);
        }

        return Task.FromResult(Build(Guid.NewGuid(), "Charge", 0m, "DOP", "OPEN"));
    }

    public Task<ChargeImportResult> ImportAsync(
        IReadOnlyList<ChargeImportRow> rows,
        bool allOrNothing,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        LastImport = rows;
        LastImportAllOrNothing = allOrNothing;

        if (Error is not null)
        {
            return Task.FromException<ChargeImportResult>(Error);
        }

        return Task.FromResult(new ChargeImportResult(rows.Count, 0, allOrNothing, []));
    }

    private static ChargeView Build(Guid id, string description, decimal amount, string currency, string status)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChargeView(
            id,
            null,
            "FACT-001",
            description,
            amount,
            currency,
            new DateOnly(2026, 3, 1),
            null,
            status,
            0m,
            amount,
            null,
            null,
            now,
            now);
    }
}
