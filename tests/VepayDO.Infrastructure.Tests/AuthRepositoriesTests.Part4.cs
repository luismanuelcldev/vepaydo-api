using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class AuthRepositoriesTests
{

    [Fact]
    public async Task SessionRepository_creates_reads_and_revokes_refresh_sessions()
    {
        var (tenantId, userId) = await SeedTenantAsync();
        var hash = RandomHash();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var sessions = new SessionRepository(host.Provider);

        var sessionId = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<Guid>(sessions.CreateAsync(
                new NewSession(
                    userId,
                    tenantId,
                    hash,
                    ExpiresAt: Clock.UtcNow.AddDays(30),
                    UserAgent: "Mozilla/5.0",
                    DeviceLabel: "Pixel 8",
                    Ip: IPAddress.Parse("190.80.1.2"),
                    CreatedAt: Clock.UtcNow),
                ct)));

        sessionId.Should().NotBeEmpty();

        var found = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindByTokenHashAsync(hash, ct)));

        found.Should().NotBeNull();
        found!.UserId.Should().Be(userId);
        found.TenantId.Should().Be(tenantId);
        found.DeviceLabel.Should().Be("Pixel 8");
        found.Ip.Should().Be(IPAddress.Parse("190.80.1.2"));
        found.RevokedAt.Should().BeNull();

        var active = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, userId, ct)));

        active.Should().NotBeNull();

        var activeForForeignUser = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, Guid.NewGuid(), ct)));

        activeForForeignUser.Should().BeNull("a session belongs to exactly one user");

        var listed = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<SessionRecord>>(
                sessions.ListActiveAsync(userId, Clock.UtcNow, ct)));

        listed.Should().ContainSingle(s => s.Id == sessionId);

        var revokedForeign = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<bool>(sessions.RevokeOwnedAsync(sessionId, Guid.NewGuid(), Clock.UtcNow, ct)));

        revokedForeign.Should().BeFalse("only the owner may revoke the session");

        var revoked = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<bool>(sessions.RevokeOwnedAsync(sessionId, userId, Clock.UtcNow, ct)));

        revoked.Should().BeTrue();

        var gone = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, userId, ct)));

        gone.Should().BeNull("the session is no longer active");
    }
}
