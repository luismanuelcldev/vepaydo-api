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

    private sealed class FakeSessionRevocationStore : ISessionRevocationStore
    {
        public List<Guid> Revoked { get; } = [];

        public Task<bool> IsRevokedAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Revoked.Contains(sessionId));

        public Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            Revoked.Add(sessionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plainPassword) => $"hash:{plainPassword}";

        public bool Verify(string passwordHash, string plainPassword) =>
            string.Equals(passwordHash, $"hash:{plainPassword}", StringComparison.Ordinal);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEventDraft> Events { get; } = [];

        public Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default)
        {
            Events.Add(draft);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
