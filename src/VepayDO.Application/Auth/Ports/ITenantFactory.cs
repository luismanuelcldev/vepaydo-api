namespace VepayDO.Application.Auth.Ports;

/// <summary>
/// Creates a tenant through <c>app.create_tenant</c>, the only supported path
/// (the runtime role has no INSERT on <c>tenants</c>). Runs inside the caller's
/// transaction so user creation and tenant creation commit together
/// (AGENTS.md sections 7 and 10).
/// </summary>
public interface ITenantFactory
{
    Task<Guid> CreateAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default);
}
