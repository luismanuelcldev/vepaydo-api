using Npgsql;
using Testcontainers.PostgreSql;
using VepayDO.Infrastructure.Migrations;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Boots a throwaway PostgreSQL 16 instance, creates the cluster-level roles as a
/// superuser (mirroring the production bootstrap), applies every SQL migration as
/// <c>vepaydo_owner</c>, and exposes the three runtime connection strings.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
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

    private async Task CreateRolesAndDatabaseAsync()
    {
        var adminConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "postgres",
            Username = "postgres",
            Password = "postgres",
        }.ConnectionString;

        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();

            // Roles are transactional and can share a single batch; CREATE DATABASE
            // cannot run inside a transaction block, so it is a separate command.
            await using (var roles = new NpgsqlCommand($"""
                CREATE ROLE {OwnerRole} LOGIN PASSWORD '{OwnerPassword}';
                CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                CREATE ROLE {MaintenanceRole} LOGIN PASSWORD '{MaintenancePassword}' BYPASSRLS;
                """, connection))
            {
                await roles.ExecuteNonQueryAsync();
            }

            await using (var database = new NpgsqlCommand(
                $"CREATE DATABASE {DatabaseName} OWNER {OwnerRole}", connection))
            {
                await database.ExecuteNonQueryAsync();
            }
        }

        var template = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = DatabaseName,
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(5432),
        };

        OwnerConnectionString = WithRole(template, OwnerRole, OwnerPassword);
        AppConnectionString = WithRole(template, AppRole, AppPassword);
        MaintenanceConnectionString = WithRole(template, MaintenanceRole, MaintenancePassword);
    }

    private static string WithRole(NpgsqlConnectionStringBuilder template, string role, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder(template.ConnectionString)
        {
            Username = role,
            Password = password,
        };

        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
