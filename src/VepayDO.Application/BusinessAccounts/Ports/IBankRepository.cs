namespace VepayDO.Application.BusinessAccounts.Ports;

public interface IBankRepository
{

    Task<BankRecord?> FindActiveAsync(short bankId, CancellationToken cancellationToken = default);
}

public sealed record BankRecord(short Id, string Code, string Name, bool IsActive);
