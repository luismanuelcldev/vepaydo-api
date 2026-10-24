using Npgsql;
using Testcontainers.PostgreSql;
using VepayDO.Infrastructure.Migrations;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class PostgresFixture
{

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
