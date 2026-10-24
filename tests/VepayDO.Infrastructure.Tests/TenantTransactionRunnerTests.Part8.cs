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
    public async Task CreateTenantAsync_creates_the_tenant_and_its_owner_membership()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        var owner = await admin.InsertUserAsync($"create-{suffix}@example.test", "New owner");

        await using var host = Host.Create(_fixture.AppConnectionString);

        var tenantId = await host.Runner.CreateTenantAsync($"Created {suffix}", owner);

        tenantId.Should().NotBeEmpty();
        host.Accessor.Current.Should().BeNull();

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, owner, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;

                var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, ct);
                tenant.Status.Should().Be("active");

                var settings = await db.TenantSettings.AsNoTracking()
                    .CountAsync(s => s.TenantId == tenantId, ct);
                settings.Should().Be(1, "app.create_tenant seeds the defaults");

                var membership = await db.Memberships.AsNoTracking()
                    .SingleAsync(m => m.TenantId == tenantId, ct);
                membership.UserId.Should().Be(owner);
                membership.Role.Should().Be(MemberRole.OWNER);
                membership.IsActive.Should().BeTrue();

                return true;
            });
    }

    [Fact]
    public async Task A_throwing_unit_of_work_is_rolled_back()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptId = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        var act = async () => await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptId));
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("boom");
            });

        await act.Should().ThrowAsync<InvalidOperationException>();

        (await CanSeeReceiptAsync(receiptId)).Should().BeFalse();
    }
}
