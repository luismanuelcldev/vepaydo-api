using Npgsql;

namespace VepayDO.Infrastructure.Migrations;

/// <summary>
/// Applies the hand-written SQL migrations in <c>db/migrations</c> in filename order,
/// each inside its own transaction, recording them in <c>infra_schema_migrations</c>.
/// It must run with the object-owner connection (<c>vepaydo_owner</c>): the database
/// roles are created by a bootstrap step, never by the migrations.
/// </summary>
public sealed class SqlMigrationRunner
{
    public const string HistoryTable = "infra_schema_migrations";

    private readonly string _connectionString;

    public SqlMigrationRunner(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// Applies every pending migration and returns the file names that were applied.
    /// </summary>
    public async Task<IReadOnlyList<string>> ApplyAsync(
        string migrationsDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsDirectory);
        if (!Directory.Exists(migrationsDirectory))
        {
            throw new DirectoryNotFoundException($"Migrations directory not found: {migrationsDirectory}");
        }

        var files = Directory.EnumerateFiles(migrationsDirectory, "*.sql")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var appliedNow = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureHistoryTableAsync(connection, cancellationToken);
        var alreadyApplied = await LoadAppliedAsync(connection, cancellationToken);

        foreach (var file in files)
        {
            if (alreadyApplied.Contains(file))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(Path.Combine(migrationsDirectory, file), cancellationToken);

            await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
            {
                await using (var command = new NpgsqlCommand(sql, connection, transaction))
                {
                    // No parameters: Npgsql sends the script through the simple query
                    // protocol, which supports multiple statements and dollar-quoted
                    // function bodies without splitting.
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var record = new NpgsqlCommand(
                    $"INSERT INTO {HistoryTable} (name) VALUES ($1)", connection, transaction))
                {
                    record.Parameters.AddWithValue(file);
                    await record.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            appliedNow.Add(file);
        }

        return appliedNow;
    }

    private static async Task EnsureHistoryTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"""
            CREATE TABLE IF NOT EXISTS {HistoryTable} (
                name       text        PRIMARY KEY,
                applied_at timestamptz NOT NULL DEFAULT now()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);

        await using var command = new NpgsqlCommand($"SELECT name FROM {HistoryTable}", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }
}
