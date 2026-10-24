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
    public async Task MembershipRepository_reads_the_users_own_memberships_under_rls()
    {
        var (tenantA, userA) = await SeedTenantAsync();
        var (tenantB, userB) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var memberships = new MembershipRepository(host.Provider, Clock);

        var own = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListActiveByUserAsync(userA, ct)));

        own.Should().ContainSingle();
        own[0].TenantId.Should().Be(tenantA);
        own[0].Role.Should().Be(MemberRole.OWNER);
        own[0].TenantName.Should().NotBeNullOrWhiteSpace();

        var foreign = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListActiveByUserAsync(userB, ct)));

        foreign.Should().BeEmpty("a user sees only its own memberships");

        var foreignTenant = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<MembershipRecord?>(memberships.FindAsync(tenantB, userA, ct)));

        foreignTenant.Should().BeNull("RLS hides tenant B from user A");

        var ownTenant = await host.Runner.RunAsUserAsync(
            userA,
            ct => new ValueTask<MembershipRecord?>(memberships.FindAsync(tenantA, userA, ct)));

        ownTenant.Should().NotBeNull();
        ownTenant!.Role.Should().Be(MemberRole.OWNER);
    }
}
