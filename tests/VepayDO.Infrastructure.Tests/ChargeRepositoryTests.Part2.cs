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
    public async Task Update_changes_the_mutable_fields_and_void_stamps_the_row()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var id = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(
                tenant.TenantId,
                new NewCharge(null, "FACT-001", "Original", 100m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
                ct)));

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await charges.UpdateAsync(
                    tenant.TenantId,
                    id,
                    new UpdateChargeFields(null, "FACT-002", "Editada", 250m, "USD", new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 9)),
                    ct);
                await charges.VoidAsync(tenant.TenantId, id, "Creada por error", tenant.OwnerId, ct);
                return true;
            });

        var record = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<ChargeRecord?>(charges.FindAsync(tenant.TenantId, id, ct)));

        record.Should().NotBeNull();
        record!.Code.Should().Be("FACT-002");
        record.Description.Should().Be("Editada");
        record.Amount.Should().Be(250m);
        record.Currency.Should().Be("USD");
        record.Status.Should().Be("VOIDED");
        record.VoidReason.Should().Be("Creada por error");
        record.VoidedAt.Should().NotBeNull();
    }
}
