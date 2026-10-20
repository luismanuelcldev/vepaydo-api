using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VepayDO.Domain.Enums;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Verifies the EF Core model matches the hand-written schema: snake_case columns,
/// PostgreSQL enum store types, and a real enum round-trip inside a tenant context.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AppDbContextTests
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

    [Fact]
    public async Task Enum_values_round_trip_through_ef_within_a_tenant_context()
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        var ownerId = await admin.InsertUserAsync($"ef-owner-{suffix}@example.test", "EF Owner");
        var tenantId = await admin.CreateTenantAsync($"EF Tenant {suffix}", ownerId);

        await using var dataSource = AppDataSource.Build(_fixture.AppConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, AppDataSource.ConfigureEnums)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.UseTransactionAsync(transaction);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, true), set_config('app.user_id', {1}, true), set_config('app.role', 'owner', true)",
                tenantId.ToString(),
                ownerId.ToString());

            db.Charges.Add(new Charge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "EF-1",
                Description = "EF round-trip",
                Amount = 100.50m,
                Currency = "DOP",
                IssuedOn = new DateOnly(2026, 1, 15),
                Status = ChargeStatus.PARTIAL,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            await db.Database.UseTransactionAsync(transaction);
            var charge = await db.Charges.SingleAsync(c => c.Code == "EF-1");
            charge.Status.Should().Be(ChargeStatus.PARTIAL);
            charge.Amount.Should().Be(100.50m);
            charge.Currency.Should().Be("DOP");
        }

        await transaction.CommitAsync();
    }
}
