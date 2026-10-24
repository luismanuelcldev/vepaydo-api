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
    public async Task UserRepository_matches_the_email_case_insensitively()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"Mixed-{suffix}@Example.Test";

        await using var host = Host.Create(_fixture.AppConnectionString);
        var users = new UserRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.CreateAsync(
                    new NewUser(Guid.NewGuid(), email, "Case", "hash:1", "es-DO", false),
                    ct);
                return true;
            });

        var found = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByEmailAsync($"MIXED-{suffix}@example.TEST", ct)));

        found.Should().NotBeNull("users.email is citext");
    }
}
