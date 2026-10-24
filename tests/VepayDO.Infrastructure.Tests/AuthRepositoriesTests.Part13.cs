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
    public async Task TenantFactory_creates_the_tenant_via_create_tenant_with_settings_and_owner()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var ownerId = await InsertUserAsync("Tenant owner");

        await using var host = Host.Create(_fixture.AppConnectionString);
        var factory = new TenantFactory(host.Provider);

        var tenantId = await host.Runner.RunAsync(
            TenantContext.None,
            ct => new ValueTask<Guid>(factory.CreateAsync($"Factory {suffix}", ownerId, ct)));

        tenantId.Should().NotBeEmpty();

        var state = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                var status = await db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Status)
                    .SingleAsync(ct);
                var settings = await db.TenantSettings.AsNoTracking()
                    .CountAsync(s => s.TenantId == tenantId, ct);
                var membership = await db.Memberships.AsNoTracking()
                    .SingleAsync(m => m.TenantId == tenantId, ct);
                return (status, settings, membership);
            });

        state.status.Should().Be("active");
        state.settings.Should().Be(1, "app.create_tenant seeds the default settings");
        state.membership.UserId.Should().Be(ownerId);
        state.membership.Role.Should().Be(MemberRole.OWNER);
        state.membership.IsActive.Should().BeTrue();
    }

    private async Task<(Guid TenantId, Guid UserId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userId = await admin.InsertUserAsync($"auth-{suffix}@example.test", $"Auth {suffix}");
        var tenantId = await admin.CreateTenantAsync($"Auth Tenant {suffix}", userId);

        return (tenantId, userId);
    }

    private async Task<Guid> InsertUserAsync(string name)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        return await admin.InsertUserAsync($"u-{suffix}@example.test", name);
    }

    private static byte[] RandomHash()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
