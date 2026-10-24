namespace VepayDO.Application.Security;

public interface ILoginAttemptStore
{

    Task<bool> IsBlockedAsync(string key, CancellationToken cancellationToken = default);

    Task RegisterFailureAsync(string key, CancellationToken cancellationToken = default);

    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}
