using FluentAssertions;
using VepayDO.Application.Security;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Security;

namespace VepayDO.Infrastructure.Tests.Security;

public sealed class SessionRevocationStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_session_is_not_revoked_before_it_is_revoked()
    {
        var store = new InMemorySessionRevocationStore(new FixedClock(Now));
        var sessionId = Guid.NewGuid();

        (await store.IsRevokedAsync(sessionId)).Should().BeFalse();
    }

    [Fact]
    public async Task Revocation_takes_effect_immediately()
    {
        var store = new InMemorySessionRevocationStore(new FixedClock(Now));
        var sessionId = Guid.NewGuid();

        await store.RevokeAsync(sessionId);

        (await store.IsRevokedAsync(sessionId)).Should().BeTrue();
    }

    [Fact]
    public async Task Revocation_expires_after_one_access_token_lifetime()
    {
        var clock = new MutableClock(Now);
        var store = new InMemorySessionRevocationStore(clock);
        var sessionId = Guid.NewGuid();

        await store.RevokeAsync(sessionId);
        (await store.IsRevokedAsync(sessionId)).Should().BeTrue();

        clock.UtcNow = Now + TokenConstants.AccessTokenLifetime + TimeSpan.FromSeconds(1);

        (await store.IsRevokedAsync(sessionId)).Should().BeFalse();
    }

    [Fact]
    public async Task Revoking_one_session_does_not_revoke_another()
    {
        var store = new InMemorySessionRevocationStore(new FixedClock(Now));
        var revoked = Guid.NewGuid();
        var untouched = Guid.NewGuid();

        await store.RevokeAsync(revoked);

        (await store.IsRevokedAsync(revoked)).Should().BeTrue();
        (await store.IsRevokedAsync(untouched)).Should().BeFalse();
    }

    private sealed class MutableClock(DateTimeOffset instant) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = instant;
    }
}
