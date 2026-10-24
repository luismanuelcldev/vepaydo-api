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
    public async Task UserRepository_round_trips_a_user_and_its_mutations()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"user-{suffix}@example.test";

        await using var host = Host.Create(_fixture.AppConnectionString);
        var users = new UserRepository(host.Provider, Clock);

        var id = Guid.NewGuid();
        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.CreateAsync(
                    new NewUser(id, email, "Ana Pérez", "hash:v1", "es-DO", MustChangePassword: true),
                    ct);
                return true;
            });

        var created = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByEmailAsync(email, ct)));

        created.Should().NotBeNull();
        created!.Id.Should().Be(id);
        created.Name.Should().Be("Ana Pérez");
        created.PasswordHash.Should().Be("hash:v1");
        created.IsActive.Should().BeTrue();
        created.PreferredLocale.Should().Be("es-DO");
        created.MustChangePassword.Should().BeTrue();
        created.IsPlatformSupport.Should().BeFalse();

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.UpdatePasswordAsync(id, "hash:v2", mustChangePassword: false, ct);
                await users.UpdateLocaleAsync(id, "en", ct);
                await users.UpdateLastLoginAsync(id, Clock.UtcNow, ct);
                await users.SetPlatformSupportAsync(id, isPlatformSupport: true, ct);
                return true;
            });

        var byId = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByIdAsync(id, ct)));

        byId.Should().NotBeNull();
        byId!.PasswordHash.Should().Be("hash:v2");
        byId.PreferredLocale.Should().Be("en");
        byId.MustChangePassword.Should().BeFalse();
        byId.IsPlatformSupport.Should().BeTrue();

        await host.Runner.RunAsync(
            TenantContext.None,
            async ct =>
            {
                await users.SetActiveAsync(id, isActive: false, ct);
                return true;
            });

        var deactivated = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByIdAsync(id, ct)));

        deactivated!.IsActive.Should().BeFalse();

        var missing = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<UserRecord?>(users.FindByEmailAsync($"absent-{suffix}@example.test", ct)));

        missing.Should().BeNull();
    }
}
