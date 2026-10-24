using Npgsql;
using Testcontainers.PostgreSql;
using VepayDO.Infrastructure.Migrations;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class PostgresFixture : IAsyncLifetime
{
    public const string OwnerRole = "vepaydo_owner";
    public const string AppRole = "vepaydo_app";
    public const string MaintenanceRole = "vepaydo_maintenance";
    public const string DatabaseName = "vepaydo";

    private const string OwnerPassword = "owner_pw";
    private const string AppPassword = "app_pw";
    private const string MaintenancePassword = "maintenance_pw";

    private PostgreSqlContainer _container = null!;

    public string OwnerConnectionString { get; private set; } = null!;

    public string AppConnectionString { get; private set; } = null!;

    public string MaintenanceConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("postgres")
            .Build();

        await _container.StartAsync();

        await CreateRolesAndDatabaseAsync();

        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "migrations");
        var runner = new SqlMigrationRunner(OwnerConnectionString);
        await runner.ApplyAsync(migrationsDirectory);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
