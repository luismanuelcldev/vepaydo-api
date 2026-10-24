using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed partial class SessionRepository
{
    public async Task<bool> RevokeOwnedAsync(
        Guid sessionId,
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var affected = await Query()
            .Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.RevokedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }

    public Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        RevokeManyAsync(Query().Where(s => s.UserId == userId && s.RevokedAt == null), at, cancellationToken);

    public Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(
        Guid userId,
        Guid keepSessionId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        RevokeManyAsync(Query().Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != keepSessionId), at, cancellationToken);

    public async Task BindTenantAsync(
        byte[] tokenHash,
        Guid userId,
        Guid tenantId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        await Query()
            .Where(s => s.TokenHash == tokenHash && s.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.TenantId, tenantId)
                    .SetProperty(s => s.LastSeenAt, lastSeenAt),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private DbSet<Session> Query() => tx.DbContext.Sessions;

    private async Task<IReadOnlyList<Guid>> RevokeManyAsync(
        IQueryable<Session> query,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var ids = await query.Select(s => s.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (ids.Count == 0) return ids;

        await tx.DbContext.Sessions
            .Where(s => ids.Contains(s.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.RevokedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

        return ids;
    }
}
