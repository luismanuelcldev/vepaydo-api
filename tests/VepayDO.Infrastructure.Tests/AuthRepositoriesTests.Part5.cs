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
    public async Task SessionRepository_revokes_every_active_session_but_keeps_the_named_one()
    {
        var (tenantId, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var sessions = new SessionRepository(host.Provider);

        async Task<Guid> CreateAsync()
            => await host.Runner.RunAsync(
                TenantContext.None,
                ct => new ValueTask<Guid>(sessions.CreateAsync(
                    new NewSession(
                        userId,
                        tenantId,
                        RandomHash(),
                        Clock.UtcNow.AddDays(30),
                        null,
                        null,
                        null,
                        Clock.UtcNow),
                    ct)));

        var keep = await CreateAsync();
        await CreateAsync();
        await CreateAsync();

        var revokedIds = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<Guid>>(
                sessions.RevokeAllExceptAsync(userId, keep, Clock.UtcNow, ct)));

        revokedIds.Should().HaveCount(2);

        var stillActive = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<SessionRecord>>(
                sessions.ListActiveAsync(userId, Clock.UtcNow, ct)));

        stillActive.Should().ContainSingle().Which.Id.Should().Be(keep);

        var afterAll = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<Guid>>(
                sessions.RevokeAllForUserAsync(userId, Clock.UtcNow, ct)));

        afterAll.Should().ContainSingle().Which.Should().Be(keep);

        var noneActive = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<IReadOnlyList<SessionRecord>>(
                sessions.ListActiveAsync(userId, Clock.UtcNow, ct)));

        noneActive.Should().BeEmpty();
    }
}
