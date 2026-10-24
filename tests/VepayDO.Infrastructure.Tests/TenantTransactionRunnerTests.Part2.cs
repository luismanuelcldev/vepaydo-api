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
    public async Task A_following_run_starts_from_a_clean_context()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptA));
                await db.SaveChangesAsync(ct);
                return true;
            });

        var visible = await host.Runner.RunAsync(
            TenantContext.None,
            async ct => await host.Provider.DbContext.Receipts.AsNoTracking()
                .AnyAsync(r => r.Id == receiptA, ct));

        visible.Should().BeFalse("the previous tenant must not leak into the next run");
    }
}
