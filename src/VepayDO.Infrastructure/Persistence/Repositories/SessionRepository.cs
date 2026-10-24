using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed partial class SessionRepository(TxProvider tx) : ISessionRepository
{
    public async Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var id = Guid.NewGuid();

        tx.DbContext.Sessions.Add(new Session
        {
            Id = id,
            UserId = session.UserId,
            TenantId = session.TenantId,
            TokenHash = session.TokenHash,
            ExpiresAt = session.ExpiresAt,
            UserAgent = session.UserAgent,
            DeviceLabel = session.DeviceLabel,
            Ip = session.Ip,
            CreatedAt = session.CreatedAt,
        });

        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<SessionRecord?> FindByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken = default)
    {
        var session = await Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        return session is null ? null : ToRecord(session);
    }

    public async Task<SessionRecord?> FindActiveByTokenHashAsync(
        byte[] tokenHash,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TokenHash == tokenHash && s.UserId == userId && s.RevokedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        return session is null ? null : ToRecord(session);
    }

    public async Task<IReadOnlyList<SessionRecord>> ListActiveAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var sessions = await Query()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sessions.ConvertAll(ToRecord);
    }

    private static SessionRecord ToRecord(Session session) => new()
    {
        Id = session.Id,
        UserId = session.UserId,
        TenantId = session.TenantId,
        ExpiresAt = session.ExpiresAt,
        RevokedAt = session.RevokedAt,
        DeviceLabel = session.DeviceLabel,
        Ip = session.Ip,
        CreatedAt = session.CreatedAt,
        LastSeenAt = session.LastSeenAt,
    };
}
