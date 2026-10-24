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

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class TenantTransactionRunnerTests
{
    private readonly PostgresFixture _fixture;

    public TenantTransactionRunnerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_scopes_reads_and_writes_to_the_context_tenant()
    {
        var (tenantA, userA, tenantB, userB) = await SeedTwoTenantsAsync();
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

        host.Accessor.Current.Should().BeNull("the context must be cleared once the run ends");

        (await CanSeeReceiptAsync(receiptA)).Should()
            .BeFalse("a connection with no tenant context fails closed");

        var visibleInB = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantB, userB, DbRole.Owner),
            async ct => await host.Provider.DbContext.Receipts.AsNoTracking()
                .AnyAsync(r => r.Id == receiptA, ct));

        visibleInB.Should().BeFalse("tenant B must not read tenant A rows");

        var visibleInA = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct => await host.Provider.DbContext.Receipts.AsNoTracking()
                .AnyAsync(r => r.Id == receiptA, ct));

        visibleInA.Should().BeTrue("the owning tenant reads its own row");
    }
}
