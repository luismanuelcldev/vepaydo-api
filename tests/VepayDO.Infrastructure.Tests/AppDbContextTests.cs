using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VepayDO.Domain.Enums;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class AppDbContextTests
{
    private readonly PostgresFixture _fixture;

    public AppDbContextTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Model_maps_snake_case_columns_and_enum_types()
    {
        await using var dataSource = AppDataSource.Build(_fixture.OwnerConnectionString);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
            .Options;
        await using var db = new AppDbContext(options);

        var receipt = db.Model.FindEntityType(typeof(Receipt))!;
        receipt.GetTableName().Should().Be("receipts");
        receipt.FindProperty(nameof(Receipt.MatchSignals))!.GetColumnName().Should().Be("match_signals");
        receipt.FindProperty(nameof(Receipt.DestinationLast4))!.GetColumnName().Should().Be("destination_last4");
        receipt.FindProperty(nameof(Receipt.ReconciliationStatus))!.GetColumnType().Should().Be("reconciliation_status");

        var membership = db.Model.FindEntityType(typeof(Membership))!;
        membership.FindProperty(nameof(Membership.Role))!.GetColumnType().Should().Be("member_role");
        membership.FindPrimaryKey()!.Properties.Select(p => p.Name)
            .Should().BeEquivalentTo([nameof(Membership.TenantId), nameof(Membership.UserId)]);

        (await db.Banks.CountAsync()).Should().Be(15);
    }
}
