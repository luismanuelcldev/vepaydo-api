using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;

namespace VepayDO.Application.Charges;

public interface IChargeService
{

    Task<IReadOnlyList<ChargeView>> ListAsync(
        ChargeQuery query,
        CancellationToken cancellationToken = default);

    Task<ChargeView> GetAsync(Guid chargeId, CancellationToken cancellationToken = default);

    Task<ChargeView> CreateAsync(
        CreateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<ChargeView> UpdateAsync(
        Guid chargeId,
        UpdateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<ChargeView> VoidAsync(
        Guid chargeId,
        VoidChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<ChargeView> DuplicateAsync(
        Guid chargeId,
        DuplicateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<ChargeImportResult> ImportAsync(
        IReadOnlyList<ChargeImportRow> rows,
        bool allOrNothing,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
