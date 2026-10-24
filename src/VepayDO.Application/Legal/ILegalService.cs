namespace VepayDO.Application.Legal;

public interface ILegalService
{
    Task<bool> HasAcceptedAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
