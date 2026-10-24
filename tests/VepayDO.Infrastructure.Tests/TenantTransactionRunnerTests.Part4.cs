using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class TenantTransactionRunnerTests
{

    [Fact]
    public void TxProvider_fails_closed_when_no_transaction_is_active()
    {
        var store = new TenantContextStore();
        var provider = new TxProvider(store);
        var accessor = new TenantContextAccessor(store);

        accessor.Current.Should().BeNull();

        Assert.Throws<MissingTenantContextException>(() => _ = provider.DbContext);
        Assert.Throws<MissingTenantContextException>(() => _ = provider.Context);
    }

    [Fact]
    public async Task RunAsUserAsync_reads_only_the_users_own_memberships_and_tenant()
    {
        var (tenantA, userA, _, userB) = await SeedTwoTenantsAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);

        var result = await host.Runner.RunAsUserAsync(userA, async ct =>
        {
            var db = host.Provider.DbContext;

            host.Provider.Context.TenantId.Should().BeNull("the pre-auth flow has no tenant");
            host.Provider.Context.UserId.Should().Be(userA);

            var membershipTenants = await db.Memberships.AsNoTracking()
                .Where(m => m.UserId == userA)
                .Select(m => m.TenantId)
                .ToListAsync(ct);

            var visibleTenants = await db.Tenants.AsNoTracking()
                .Select(t => t.Id)
                .ToListAsync(ct);

            var foreignMembership = await db.Memberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userB, ct);

            var anyBusinessRow = await db.Receipts.AsNoTracking().AnyAsync(ct);

            return (membershipTenants, visibleTenants, foreignMembership, anyBusinessRow);
        });

        result.membershipTenants.Should().ContainSingle().Which.Should().Be(tenantA);
        result.visibleTenants.Should().BeEquivalentTo([tenantA]);
        result.foreignMembership.Should().BeFalse();
        result.anyBusinessRow.Should()
            .BeFalse("tenant-scoped tables stay invisible until a tenant is set");
    }
}
