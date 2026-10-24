using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VepayDO.Domain.Enums;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class AppDbContextTests
{

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
