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
    public async Task SessionRepository_binds_the_chosen_tenant_to_the_session()
    {
        var (tenantId, userId) = await SeedTenantAsync();
        var hash = RandomHash();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var sessions = new SessionRepository(host.Provider);

        await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<Guid>(sessions.CreateAsync(
                new NewSession(userId, null, hash, Clock.UtcNow.AddDays(30), null, null, null, Clock.UtcNow),
                ct)));

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await sessions.BindTenantAsync(hash, userId, tenantId, Clock.UtcNow, ct);
                return true;
            });

        var bound = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<SessionRecord?>(sessions.FindActiveByTokenHashAsync(hash, userId, ct)));

        bound!.TenantId.Should().Be(tenantId);
        bound.LastSeenAt.Should().Be(Clock.UtcNow);
    }
}
