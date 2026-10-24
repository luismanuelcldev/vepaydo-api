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
    public async Task Concurrent_flows_keep_their_own_context()
    {
        var (tenantA, userA, tenantB, userB) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();
        var receiptB = Guid.NewGuid();

        await using (var seedHost = Host.Create(_fixture.AppConnectionString))
        {
            await seedHost.Runner.RunAsync(
                TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
                async ct =>
                {
                    var db = seedHost.Provider.DbContext;
                    db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                    await db.SaveChangesAsync(ct);
                    return true;
                });

            await seedHost.Runner.RunAsync(
                TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
                async ct =>
                {
                    var db = seedHost.Provider.DbContext;
                    db.Receipts.Add(NewReceipt(tenantB, userB, receiptB));
                    await db.SaveChangesAsync(ct);
                    return true;
                });
        }

        var store = new TenantContextStore();
        await using var hostA = Host.Create(_fixture.AppConnectionString, store);
        await using var hostB = Host.Create(_fixture.AppConnectionString, store);

        async Task<bool> Observe(Host host, TenantContext context, Guid own, Guid foreign)
            => await host.Runner.RunAsync(context, async ct =>
            {
                var db = host.Provider.DbContext;

                var ownVisible = await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == own, ct);
                var foreignVisible = await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == foreign, ct);

                return ownVisible && !foreignVisible;
            });

        var first = Task.Run(() => Observe(
            hostA,
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            receiptA,
            receiptB));

        var second = Task.Run(() => Observe(
            hostB,
            TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
            receiptB,
            receiptA));

        await Task.WhenAll(first, second);

        (await first).Should().BeTrue("flow A must see only tenant A");
        (await second).Should().BeTrue("flow B must see only tenant B");
    }
}
