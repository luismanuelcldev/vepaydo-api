using FluentAssertions;
using VepayDO.Application.Security;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Security;

namespace VepayDO.Infrastructure.Tests.Security;

public sealed class LoginAttemptStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class MutableClock(DateTimeOffset instant) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = instant;
    }

    [Fact]
    public async Task A_key_with_no_failures_is_not_blocked()
    {
        var store = new InMemoryLoginAttemptStore(new MutableClock(Now));

        (await store.IsBlockedAsync("user@example.com")).Should().BeFalse();
    }

    [Fact]
    public async Task Five_failures_block_the_key()
    {
        var store = new InMemoryLoginAttemptStore(new MutableClock(Now));
        const string key = "user@example.com";

        for (var i = 0; i < InMemoryLoginAttemptStore.MaxAttempts; i++)
        {
            (await store.IsBlockedAsync(key)).Should().BeFalse();
            await store.RegisterFailureAsync(key);
        }

        (await store.IsBlockedAsync(key)).Should().BeTrue();
    }

    [Fact]
    public async Task A_successful_login_clears_the_counter()
    {
        var store = new InMemoryLoginAttemptStore(new MutableClock(Now));
        const string key = "user@example.com";

        for (var i = 0; i < InMemoryLoginAttemptStore.MaxAttempts - 1; i++)
        {
            await store.RegisterFailureAsync(key);
        }

        await store.ResetAsync(key);

        (await store.IsBlockedAsync(key)).Should().BeFalse();
        await store.RegisterFailureAsync(key);
        (await store.IsBlockedAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task The_block_expires_when_the_window_elapses()
    {
        var clock = new MutableClock(Now);
        var store = new InMemoryLoginAttemptStore(clock);
        const string key = "user@example.com";

        for (var i = 0; i < InMemoryLoginAttemptStore.MaxAttempts; i++)
        {
            await store.RegisterFailureAsync(key);
        }

        (await store.IsBlockedAsync(key)).Should().BeTrue();

        clock.UtcNow = Now + InMemoryLoginAttemptStore.Window + TimeSpan.FromSeconds(1);

        (await store.IsBlockedAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Keys_are_tracked_independently()
    {
        var store = new InMemoryLoginAttemptStore(new MutableClock(Now));

        for (var i = 0; i < InMemoryLoginAttemptStore.MaxAttempts; i++)
        {
            await store.RegisterFailureAsync("a@example.com");
        }

        (await store.IsBlockedAsync("a@example.com")).Should().BeTrue();
        (await store.IsBlockedAsync("b@example.com")).Should().BeFalse();
    }
}
