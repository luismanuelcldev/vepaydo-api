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
    public async Task PasswordResetTokenRepository_invalidates_every_unused_token_for_the_user()
    {
        var (_, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var tokens = new PasswordResetTokenRepository(host.Provider, Clock);

        for (var i = 0; i < 3; i++)
        {
            await host.Runner.RunAsync(
                TenantContext.None,
                async ct =>
                {
                    await tokens.CreateAsync(
                        new NewPasswordResetToken(
                            userId,
                            RandomHash(),
                            Clock.UtcNow.AddMinutes(30),
                            null,
                            null),
                        ct);
                    return true;
                });
        }

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await tokens.InvalidateUnusedForUserAsync(userId, Clock.UtcNow, ct);
                return true;
            });

        var unused = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<int>(
                host.Provider.DbContext.PasswordResetTokens.AsNoTracking()
                    .CountAsync(t => t.UserId == userId && t.UsedAt == null, ct)));

        unused.Should().Be(0);
    }
}
