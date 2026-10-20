using Npgsql;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Thin test helper over a single Npgsql connection. A tenant context is opened
/// with <see cref="UseContextAsync"/>, which begins a transaction so the
/// <c>set_config(..., true)</c> values stay local to it, exactly like the runtime
/// <c>TenantTransactionRunner</c> will.
/// </summary>
internal sealed class DbClient : IAsyncDisposable
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

    public async Task<Guid> InsertChargeAsync(string code, decimal amount, string currency = "DOP")
    {
        var value = await ScalarAsync(
            """
            INSERT INTO charges (tenant_id, code, description, amount, currency, updated_at)
            VALUES (app.current_tenant_id(), $1, $1, $2, $3, now())
            RETURNING id
            """, code, amount, currency);

        return (Guid)value!;
    }

    public async Task<Guid> InsertReceiptAsync(decimal? amount = null, string currency = "DOP", byte[]? sha = null)
    {
        var value = await ScalarAsync(
            """
            INSERT INTO receipts (tenant_id, idempotency_key, uploaded_by, source, storage_key,
                                  mime, bytes, sha256, amount, currency, updated_at)
            VALUES (app.current_tenant_id(), gen_random_uuid(), app.current_user_id(), 'WEB_UPLOAD', 'k',
                    'image/png', 10, $1, $2, $3, now())
            RETURNING id
            """, sha ?? new byte[32], amount, amount is null ? null : currency);

        return (Guid)value!;
    }

    public async Task CommitAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }

    private static void AddParameters(NpgsqlCommand command, object?[] parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue(parameters[i] ?? DBNull.Value);
        }
    }
}
