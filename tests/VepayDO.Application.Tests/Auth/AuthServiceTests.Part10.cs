using VepayDO.Application.Auth.Ports;

namespace VepayDO.Application.Tests.Auth;

public sealed partial class AuthServiceTests
{
    private sealed class FakeSessionRepository : ISessionRepository
    {
        private readonly List<(SessionRecord Record, byte[] Hash)> _sessions = [];
        public IReadOnlyList<SessionRecord> All => _sessions.Select(entry => entry.Record).ToList();
        public void Seed(SessionRecord record, byte[] hash) => _sessions.Add((record, hash));

        public Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(session);
            var id = Guid.NewGuid();
            _sessions.Add((new SessionRecord { Id = id, UserId = session.UserId, TenantId = session.TenantId, ExpiresAt = session.ExpiresAt, DeviceLabel = session.DeviceLabel, CreatedAt = session.CreatedAt }, session.TokenHash));
            return Task.FromResult(id);
        }

        public Task<SessionRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default) => Task.FromResult(Find(tokenHash, userId: null));
        public Task<SessionRecord?> FindActiveByTokenHashAsync(byte[] tokenHash, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Find(tokenHash, userId));

        public Task<IReadOnlyList<SessionRecord>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SessionRecord> active = _sessions.Where(e => e.Record.UserId == userId && e.Record.RevokedAt is null && e.Record.ExpiresAt > now).Select(e => e.Record).ToList();
            return Task.FromResult(active);
        }

        public Task<bool> RevokeOwnedAsync(Guid sessionId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var index = _sessions.FindIndex(e => e.Record.Id == sessionId && e.Record.UserId == userId && e.Record.RevokedAt is null);
            if (index < 0) return Task.FromResult(false);
            _sessions[index] = (_sessions[index].Record with { RevokedAt = at }, _sessions[index].Hash);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Revoke(e => e.Record.UserId == userId, at));

        public Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(Guid userId, Guid keepSessionId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Revoke(e => e.Record.UserId == userId && e.Record.Id != keepSessionId, at));

        public Task BindTenantAsync(byte[] tokenHash, Guid userId, Guid tenantId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
        {
            var index = _sessions.FindIndex(e => e.Hash.AsSpan().SequenceEqual(tokenHash) && e.Record.UserId == userId);
            if (index >= 0) _sessions[index] = (_sessions[index].Record with { TenantId = tenantId, LastSeenAt = lastSeenAt }, _sessions[index].Hash);
            return Task.CompletedTask;
        }

        private List<Guid> Revoke(Func<(SessionRecord Record, byte[] Hash), bool> predicate, DateTimeOffset at)
        {
            var ids = new List<Guid>();
            for (var i = 0; i < _sessions.Count; i++)
            {
                var e = _sessions[i];
                if (e.Record.RevokedAt is null && predicate(e)) { ids.Add(e.Record.Id); _sessions[i] = (e.Record with { RevokedAt = at }, e.Hash); }
            }
            return ids;
        }

        private SessionRecord? Find(byte[] tokenHash, Guid? userId) =>
            _sessions.FirstOrDefault(e => e.Hash.AsSpan().SequenceEqual(tokenHash) && (userId is null || e.Record.UserId == userId) && (userId is null || e.Record.RevokedAt is null)).Record;
    }
}
