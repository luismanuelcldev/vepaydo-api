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
    public async Task ActivateAsync_switches_the_tenant_inside_the_running_transaction()
    {
        var (tenantA, userA, tenantB, userB) = await SeedTwoTenantsAsync();
        var receiptA = Guid.NewGuid();
        var receiptB = Guid.NewGuid();

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

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantB, userB, receiptB));
                await db.SaveChangesAsync(ct);
                return true;
            });

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;

                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptA, ct))
                    .Should().BeTrue();
                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptB, ct))
                    .Should().BeFalse();

                await host.Runner.ActivateAsync(
                    TenantContext.ForTenant(tenantB, userB, DbRole.Owner), ct);

                host.Accessor.Current!.TenantId.Should().Be(tenantB);

                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptB, ct))
                    .Should().BeTrue();
                (await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptA, ct))
                    .Should().BeFalse();

                return true;
            });
    }

    private async Task<bool> CanSeeReceiptAsync(Guid receiptId)
    {
        await using var dataSource = AppDataSource.Build(_fixture.AppConnectionString);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
            .Options;

        await using var db = new AppDbContext(options);
        return await db.Receipts.AsNoTracking().AnyAsync(r => r.Id == receiptId);
    }
}
