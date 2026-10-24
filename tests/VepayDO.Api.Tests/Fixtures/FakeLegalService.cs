using VepayDO.Application.Legal;

namespace VepayDO.Api.Tests.Fixtures;

public sealed class FakeLegalService : ILegalService
{
    public bool Accepted { get; set; } = true;

    public int CallCount { get; private set; }

    public Task<bool> HasAcceptedAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Accepted);
    }
}
