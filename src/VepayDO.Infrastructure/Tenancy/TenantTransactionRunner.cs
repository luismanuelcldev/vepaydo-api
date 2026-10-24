using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence;

namespace VepayDO.Infrastructure.Tenancy;

public sealed class TenantTransactionRunner : ITenantTransactionRunner
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);
    private readonly AppDbContext _db;
    private readonly TenantContextStore _store;

    public TenantTransactionRunner(AppDbContext db, TenantContextStore store)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _db.Database.SetCommandTimeout(CommandTimeout);
    }

    public Task<TResult> RunAsync<TResult>(
        TenantContext context,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        var scope = new TenantScope(context, _db);
        return _store.RunAsync(scope, async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ApplyContextAsync(context, cancellationToken).ConfigureAwait(false);
            var result = await work(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        });
    }

    public async Task RunAsync(TenantContext context, Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await RunAsync(context, async ct => { await work(ct).ConfigureAwait(false); return true; }, cancellationToken).ConfigureAwait(false);
    }

    public Task<TResult> RunAsUserAsync<TResult>(Guid userId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default)
        => RunAsync(TenantContext.ForUser(userId), work, cancellationToken);

    public Task<TResult> RunAsSystemAsync<TResult>(Guid tenantId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken = default)
        => RunAsync(TenantContext.ForSystem(tenantId), work, cancellationToken);

    public async Task ActivateAsync(TenantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = _store.RequireScope();
        scope.Context = context;
        await ApplyContextAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid> CreateTenantAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var ids = await _db.Database
            .SqlQuery<Guid>($"SELECT app.create_tenant({name}::text, {ownerUserId}::uuid) AS \"tenant_id\"")
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (ids.Count != 1 || ids[0] == Guid.Empty)
            throw new InvalidOperationException("app.create_tenant did not return a tenant id");

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ids[0];
    }

    private async Task ApplyContextAsync(TenantContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;
        var userId = context.UserId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;
        var role = context.Role?.ToDatabaseValue() ?? string.Empty;

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId}, true), set_config('app.user_id', {userId}, true), set_config('app.role', {role}, true)",
            cancellationToken).ConfigureAwait(false);
    }
}
