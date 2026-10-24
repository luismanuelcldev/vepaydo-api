using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class BusinessAccountRepositoryTests
{
    private static readonly FixedClock Clock = FixedClock.At("2026-03-01T12:00:00Z");

    private readonly PostgresFixture _fixture;

    public BusinessAccountRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_and_list_round_trip_the_last_four_digits()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var accounts = new BusinessAccountRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        var created = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<Guid>(accounts.AddAsync(
                tenant.TenantId,
                new NewBusinessAccount(BankId: 1, "Principal", "4321", "DOP", IsActive: true),
                ct)));

        var list = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<IReadOnlyList<BusinessAccountRecord>>(accounts.ListAsync(tenant.TenantId, ct)));

        list.Should().ContainSingle();
        list[0].Id.Should().Be(created);
        list[0].Last4.Should().Be("4321");
        list[0].Currency.Should().Be("DOP");
        list[0].BankName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Exists_duplicate_detects_the_unique_index_pair()
    {
        var tenant = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var accounts = new BusinessAccountRepository(host.Provider, Clock);
        var context = TenantContext.ForTenant(tenant.TenantId, tenant.OwnerId, DbRole.Owner);

        await host.Runner.RunAsync(
            context,
            async ct =>
            {
                await accounts.AddAsync(
                    tenant.TenantId,
                    new NewBusinessAccount(1, "Principal", "4321", "DOP", IsActive: true),
                    ct);
                return true;
            });

        var duplicate = await host.Runner.RunAsync(
            context,
            ct => new ValueTask<bool>(accounts.ExistsDuplicateAsync(tenant.TenantId, 1, "4321", "DOP", excludingAccountId: null, ct)));

        duplicate.Should().BeTrue();
    }
}
