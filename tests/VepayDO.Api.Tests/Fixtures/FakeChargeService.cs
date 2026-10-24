using VepayDO.Application.Auth;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;

namespace VepayDO.Api.Tests.Fixtures;

/// <summary>
/// Configuration-driven stand-in for <see cref="IChargeService"/>. The real service
/// needs PostgreSQL; these tests exercise the HTTP contract (verbs, status codes,
/// permission gating, validation, CSV import) around it.
/// </summary>
public sealed class FakeChargeService : IChargeService
{
    public IReadOnlyList<ChargeView> Charges { get; set; } = [];

    /// <summary>When set, every call throws instead of returning a result.</summary>
    public Exception? Error { get; set; }

    /// <summary>Rows received by the last import call.</summary>
    public IReadOnlyList<ChargeImportRow>? LastImport { get; private set; }

    /// <summary>All-or-nothing flag received by the last import call.</summary>
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
