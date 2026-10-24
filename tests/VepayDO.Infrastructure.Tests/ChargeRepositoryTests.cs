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

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class ChargeRepositoryTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public ChargeRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_and_list_charges_round_trip_with_zero_balance_applied()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var charges = new ChargeRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var id = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(charges.AddAsync(
                tenant.TenantId,
                new NewCharge(null, "FACT-001", "Servicio", 1500m, "DOP", new DateOnly(2026, 3, 1), null, tenant.OwnerId),
                ct)));

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<ChargeRecord>>(charges.ListAsync(tenant.TenantId, new ChargeQuery(null, null), ct)));

        list.Should().ContainSingle();
        list[0].Id.Should().Be(id);
        list[0].Code.Should().Be("FACT-001");
        list[0].Status.Should().Be("OPEN");
        list[0].AmountApplied.Should().Be(0m);
        list[0].Balance.Should().Be(1500m);
    }
}
