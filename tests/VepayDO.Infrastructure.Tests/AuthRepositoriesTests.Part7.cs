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
    public async Task PasswordResetTokenRepository_stores_only_the_hash_and_is_single_use()
    {
        var (_, userId) = await SeedTenantAsync();
        var hash = RandomHash();
        var future = Clock.UtcNow.AddMinutes(30);

        await using var host = Host.Create(_fixture.AppConnectionString);
        var tokens = new PasswordResetTokenRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await tokens.CreateAsync(
                    new NewPasswordResetToken(userId, hash, future, IPAddress.Parse("10.0.0.9"), null),
                    ct);
                return true;
            });

        var found = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<PasswordResetTokenRecord?>(tokens.FindByTokenHashAsync(hash, ct)));

        found.Should().NotBeNull();
        found!.UserId.Should().Be(userId);
        found.UsedAt.Should().BeNull();
        found.ExpiresAt.Should().Be(future);

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await tokens.MarkUsedAsync(found.Id, Clock.UtcNow, ct);
                return true;
            });

        var used = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<PasswordResetTokenRecord?>(tokens.FindByTokenHashAsync(hash, ct)));

        used!.UsedAt.Should().Be(Clock.UtcNow);

        var rawHash = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<string>(
                host.Provider.DbContext.PasswordResetTokens.AsNoTracking()
                    .Where(t => t.Id == found.Id)
                    .Select(t => t.RequestedIp!.ToString())
                    .SingleAsync(ct)));

        rawHash.Should().Be("10.0.0.9");
    }
}
