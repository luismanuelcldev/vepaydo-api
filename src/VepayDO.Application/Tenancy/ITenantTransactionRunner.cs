namespace VepayDO.Application.Tenancy;

/// <summary>
/// Executes a unit of work inside a single PostgreSQL transaction with the
/// tenant context installed via <c>set_config</c> (AGENTS.md section 7).
/// </summary>
public interface ITenantTransactionRunner
{
    Task<TResult> RunAsync<TResult>(
        TenantContext context,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default);

    Task RunAsync(
        TenantContext context,
        Func<CancellationToken, ValueTask> work,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsUserAsync<TResult>(
        Guid userId,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsSystemAsync<TResult>(
        Guid tenantId,
        Func<CancellationToken, ValueTask<TResult>> work,
        CancellationToken cancellationToken = default);

    Task ActivateAsync(
        TenantContext context,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateTenantAsync(
        string name,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);
}
