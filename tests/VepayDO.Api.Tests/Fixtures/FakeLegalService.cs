using VepayDO.Application.Legal;

namespace VepayDO.Api.Tests.Fixtures;

/// <summary>
/// Legal acceptance ledger stand-in. The real table is global and needs
/// PostgreSQL; the gate behaviour is what these tests exercise.
/// </summary>
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
