namespace VepayDO.Application.Security;

public interface ISessionRevocationStore
{
    Task<bool> IsRevokedAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
