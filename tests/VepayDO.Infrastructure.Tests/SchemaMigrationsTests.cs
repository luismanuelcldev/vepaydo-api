using FluentAssertions;
using VepayDO.Infrastructure.Migrations;

namespace VepayDO.Infrastructure.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class SchemaMigrationsTests
{
    private static readonly string[] ExpectedTables =
    [
        "banks", "users", "sessions", "tenants", "tenant_settings", "memberships",
        "push_devices", "business_accounts", "customers", "customer_aliases", "charges",
        "receipts", "extractions", "allocations", "alerts", "audit_events", "ux_events",
        "support_grants", "password_reset_tokens", "legal_acceptances",
    ];

    private static readonly string[] ExpectedRlsTables =
    [
        "tenants", "tenant_settings", "memberships", "push_devices", "business_accounts",
        "customers", "customer_aliases", "charges", "receipts", "extractions", "allocations",
        "alerts", "audit_events", "ux_events", "support_grants",
    ];

    private readonly PostgresFixture _fixture;

    public SchemaMigrationsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Every_migration_is_applied_once()
    {
        await using var owner = await DbClient.ConnectAsync(_fixture.OwnerConnectionString);
        var applied = await owner.CountAsync(SqlMigrationRunner.HistoryTable);
        applied.Should().Be(11);
    }

    [Fact]
    public async Task All_application_tables_exist()
    {
        await using var owner = await DbClient.ConnectAsync(_fixture.OwnerConnectionString);

        foreach (var table in ExpectedTables)
        {
            var exists = await owner.ScalarAsync("SELECT to_regclass($1) IS NOT NULL", "public." + table);
            exists.Should().Be(true, $"table {table} should exist");
        }
    }

    [Fact]
    public async Task Required_extensions_are_installed()
    {
        await using var owner = await DbClient.ConnectAsync(_fixture.OwnerConnectionString);

        var citext = await owner.ScalarAsync("SELECT count(*) FROM pg_extension WHERE extname = 'citext'");
        var trigram = await owner.ScalarAsync("SELECT count(*) FROM pg_extension WHERE extname = 'pg_trgm'");

        Convert.ToInt64(citext!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(1);
        Convert.ToInt64(trigram!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(1);
    }

    [Fact]
    public async Task Bank_catalog_is_seeded()
    {
        await using var owner = await DbClient.ConnectAsync(_fixture.OwnerConnectionString);
        var banks = await owner.CountAsync("banks");
        banks.Should().Be(15);
    }
}
