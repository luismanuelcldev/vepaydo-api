using Npgsql;

namespace VepayDO.Infrastructure.Tests;

internal sealed partial class DbClient
{

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
