using VepayDO.Application.Tenancy;

namespace VepayDO.Api.Tests.Fixtures;

/// <summary>
/// Records every tenant context the pipeline installs, without touching a
/// database. The M4 tests are about the HTTP kernel, not about PostgreSQL, so
/// this replaces the real transaction runner.
/// </summary>
public sealed class RecordingTenantTransactionRunner : ITenantTransactionRunner
{
    public List<TenantContext> RunContexts { get; } = [];

    public TenantContext? LastContext => RunContexts.Count == 0 ? null : RunContexts[^1];

    public async Task<TResult> RunAsync<TResult>(
        TenantContext context,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        RunContexts.Add(context);
        return await work(cancellationToken).ConfigureAwait(false);
    }

    public Task RunAsync(
        TenantContext context,
        Func<CancellationToken, ValueTask> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        return RunAsync(
            context,
            async ct =>
            {
                await work(ct).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    public Task<TResult> RunAsUserAsync<TResult>(
        Guid userId,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default)
        => RunAsync(TenantContext.ForUser(userId), work, cancellationToken);

    public Task<TResult> RunAsSystemAsync<TResult>(
        Guid tenantId,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default)
        => RunAsync(TenantContext.ForSystem(tenantId), work, cancellationToken);

    public Task ActivateAsync(TenantContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<Guid> CreateTenantAsync(
        string name,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Guid.NewGuid());
}
