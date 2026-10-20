namespace VepayDO.Application.Legal;

/// <summary>
/// Reads the immutable <c>legal_acceptances</c> ledger (UC-AUTH-08). The gate
/// asks whether the user has accepted the versions currently configured, so a
/// version bump re-blocks every account until it accepts again.
/// </summary>
public interface ILegalService
{
    Task<bool> HasAcceptedAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
