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
    public async Task Add_many_inserts_all_rows_in_one_transaction()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var rows = new List<NewCharge>
        {
            new(null, "A-1", "Uno", 10m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
            new(null, "A-2", "Dos", 20m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
        };

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await charges.AddManyAsync(tenant.TenantId, rows, ct);
                return true;
            });

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<ChargeRecord>>(charges.ListAsync(tenant.TenantId, new ChargeQuery(null, null), ct)));

        list.Should().HaveCount(2);
        list.Select(c => c.Code).Should().BeEquivalentTo(["A-1", "A-2"]);
    }

    [Fact]
    public async Task A_duplicate_code_is_rejected_by_the_unique_index()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var charge = new NewCharge(
            null, "FACT-001", "Servicio", 100m, "DOP", new DateOnly(2026, 3, 1), null, null);

        await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(tenant.TenantId, charge, ct)));

        var act = async () => await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(tenant.TenantId, charge, ct)));

        await act.Should().ThrowAsync<ConflictException>();
    }
}
