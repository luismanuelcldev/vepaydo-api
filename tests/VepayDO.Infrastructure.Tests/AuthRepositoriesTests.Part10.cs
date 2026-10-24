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
    public async Task MembershipRepository_lists_and_updates_members_within_the_tenant()
    {
        var (tenantId, ownerId) = await SeedTenantAsync();
        var verifierId = await InsertUserAsync("Verifier");

        await using var host = Host.Create(_fixture.AppConnectionString);
        var memberships = new MembershipRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                await memberships.AddAsync(tenantId, verifierId, MemberRole.VERIFIER, ct);
                return true;
            });

        var listed = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListByTenantAsync(tenantId, ct)));

        listed.Should().HaveCount(2);
        var verifier = listed.Should().ContainSingle(m => m.UserId == verifierId).Which;
        verifier.Role.Should().Be(MemberRole.VERIFIER);
        verifier.UserEmail.Should().NotBeNullOrWhiteSpace();

        var ownerCount = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<int>(memberships.CountActiveOwnersAsync(tenantId, ct)));

        ownerCount.Should().Be(1);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                await memberships.UpdateRoleAsync(tenantId, verifierId, MemberRole.OWNER, ct);
                return true;
            });

        var owners = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<int>(memberships.CountActiveOwnersAsync(tenantId, ct)));

        owners.Should().Be(2);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            async ct =>
            {
                await memberships.SetActiveAsync(tenantId, verifierId, isActive: false, ct);
                return true;
            });

        var active = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, ownerId, DbRole.Owner),
            ct => new ValueTask<int>(memberships.CountActiveOwnersAsync(tenantId, ct)));

        active.Should().Be(1, "the deactivated member no longer counts as an active owner");
    }
}
