using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Members;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Members;

public sealed partial class MemberServiceTests
{

    private sealed class FakeSessionRepository : ISessionRepository
    {
        private readonly List<SessionRecord> _sessions = [];

        public void Seed(Guid userId, Guid sessionId)
        {
            _sessions.Add(new SessionRecord
            {
                Id = sessionId,
                UserId = userId,
                ExpiresAt = Now.AddDays(30),
                CreatedAt = Now,
            });
        }

        public bool RevokedFor(Guid userId) =>
            _sessions.Where(s => s.UserId == userId).All(s => s.RevokedAt is not null);

        public Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SessionRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SessionRecord?> FindActiveByTokenHashAsync(byte[] tokenHash, Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SessionRecord>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RevokeOwnedAsync(Guid sessionId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var ids = new List<Guid>();
            for (var index = 0; index < _sessions.Count; index++)
            {
                var session = _sessions[index];
                if (session.UserId == userId && session.RevokedAt is null)
                {
                    _sessions[index] = session with { RevokedAt = at };
                    ids.Add(session.Id);
                }
            }

            return Task.FromResult<IReadOnlyList<Guid>>(ids);
        }

        public Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(Guid userId, Guid keepSessionId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task BindTenantAsync(byte[] tokenHash, Guid userId, Guid tenantId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
