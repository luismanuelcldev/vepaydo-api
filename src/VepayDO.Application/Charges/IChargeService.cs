using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;

namespace VepayDO.Application.Charges;

/// <summary>
/// Charge use cases (UC-DATA-03 to 06). Read and mutate are allowed for OWNER and
/// VERIFIER (<c>charges.manage</c>). Codes are unique per tenant; a collision is
/// retried with a freshly generated code. Every mutation is audited in the same
/// transaction.
/// </summary>
public interface IChargeService
{
    /// <summary>Lists the current tenant's charges with their balances.</summary>
    Task<IReadOnlyList<ChargeView>> ListAsync(
        ChargeQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one charge or throws when it does not belong to the tenant.</summary>
    Task<ChargeView> GetAsync(Guid chargeId, CancellationToken cancellationToken = default);

    /// <summary>Creates a charge, generating the code when none is supplied.</summary>
    Task<ChargeView> CreateAsync(
        CreateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an open charge; a voided charge cannot be edited.</summary>
    Task<ChargeView> UpdateAsync(
        Guid chargeId,
        UpdateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Voids a charge with a mandatory reason.</summary>
    Task<ChargeView> VoidAsync(
        Guid chargeId,
        VoidChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Duplicates a charge via the ChargeDraft prototype (UC-DATA-04).</summary>
    Task<ChargeView> DuplicateAsync(
        Guid chargeId,
        DuplicateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Imports parsed CSV rows with a per-row error report (UC-DATA-06).</summary>
    Task<ChargeImportResult> ImportAsync(
        IReadOnlyList<ChargeImportRow> rows,
        bool allOrNothing,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
