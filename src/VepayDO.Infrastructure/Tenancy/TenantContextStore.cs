using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence;

namespace VepayDO.Infrastructure.Tenancy;

public sealed class TenantContextStore
{
    private readonly AsyncLocal<TenantScope?> _current = new();

    public TenantContext? Context => _current.Value?.Context;

    public AppDbContext? DbContext => _current.Value?.Db;

    internal TenantScope RequireScope()
    {
        return _current.Value ?? throw new MissingTenantContextException();
    }

    internal async Task<TResult> RunAsync<TResult>(TenantScope scope, Func<Task<TResult>> work)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(work);

        var previous = _current.Value;
        _current.Value = scope;

        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            _current.Value = previous;
        }
    }
}
