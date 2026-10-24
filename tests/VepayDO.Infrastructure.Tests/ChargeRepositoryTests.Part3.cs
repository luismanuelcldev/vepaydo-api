using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class ChargeRepositoryTests
{

    [Fact]
    public async Task Code_exists_is_scoped_to_the_tenant_and_can_exclude_self()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var id = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(
                tenant.TenantId,
                new NewCharge(null, "FACT-001", "Servicio", 10m, "DOP", new DateOnly(2026, 3, 1), null, null),
                ct)));

        var exists = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<bool>(charges.CodeExistsAsync(tenant.TenantId, "FACT-001", null, ct)));

        var existsExcludingSelf = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<bool>(charges.CodeExistsAsync(tenant.TenantId, "FACT-001", id, ct)));

        exists.Should().BeTrue();
        existsExcludingSelf.Should().BeFalse();
    }

    [Fact]
    public async Task Balance_reflects_active_allocations_from_the_view()
    {
        var tenant = await SeedTenantAsync();

        Guid chargeId;
        await using (var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await admin.UseContextAsync(tenant.TenantId, tenant.OwnerId, "owner");
            chargeId = await admin.InsertChargeAsync("FACT-001", 1000m);
            var receiptId = await admin.InsertReceiptAsync(400m);
            await admin.ExecuteAsync(
                """
                INSERT INTO allocations (tenant_id, receipt_id, charge_id, amount_applied, created_by)
                VALUES (app.current_tenant_id(), $1, $2, 400, app.current_user_id())
                """, receiptId, chargeId);
            await admin.CommitAsync();
        }

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var record = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<ChargeRecord?>(charges.FindAsync(tenant.TenantId, chargeId, ct)));

        record.Should().NotBeNull();
        record!.Amount.Should().Be(1000m);
        record.AmountApplied.Should().Be(400m);
        record.Balance.Should().Be(600m);
    }
}
