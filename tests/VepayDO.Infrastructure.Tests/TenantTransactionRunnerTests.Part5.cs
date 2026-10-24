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
    public async Task An_empty_context_sees_no_rows_at_all()
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

        var seen = await host.Runner.RunAsync(TenantContext.None, async ct =>
        {
            var db = host.Provider.DbContext;
            var receipts = await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptA, ct);
            var memberships = await db.Memberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userA, ct);
            return (receipts, memberships);
        });

        seen.receipts.Should().BeFalse();
        seen.memberships.Should().BeFalse();
    }
}
