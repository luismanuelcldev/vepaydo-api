using Npgsql;

namespace VepayDO.Infrastructure.Tests;

internal sealed partial class DbClient : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;

    private DbClient(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public static async Task<DbClient> ConnectAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return new DbClient(connection);
    }

    public async Task UseContextAsync(Guid? tenantId, Guid? userId, string role)
    {
        _transaction ??= (NpgsqlTransaction)await _connection.BeginTransactionAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT set_config('app.tenant_id', $1, true),
                   set_config('app.user_id',   $2, true),
                   set_config('app.role',      $3, true)
            """, _connection, _transaction);
        command.Parameters.AddWithValue(tenantId?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue(userId?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue(role);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> ExecuteAsync(string sql, params object?[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, _connection, _transaction);
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql, params object?[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, _connection, _transaction);
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync();
    }

    public async Task<long> CountAsync(string table)
    {
        var value = await ScalarAsync($"SELECT count(*) FROM {table}");
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<Guid> InsertUserAsync(string email, string name)
    {
        var value = await ScalarAsync(
            "INSERT INTO users (email, name, password_hash) VALUES ($1, $2, 'x') RETURNING id",
            email, name);

        return (Guid)value!;
    }

    public async Task<Guid> CreateTenantAsync(string name, Guid ownerUserId)
    {
        var value = await ScalarAsync("SELECT app.create_tenant($1, $2)", name, ownerUserId);
        return (Guid)value!;
    }
}
