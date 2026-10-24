namespace VepayDO.Application.Auth.Ports;

public interface ITenantFactory
{
    Task<Guid> CreateAsync(string name, Guid ownerUserId, CancellationToken cancellationToken = default);
}
