namespace VepayDO.Application.BusinessAccounts.Ports;

/// <summary>
/// Read-only access to the global <c>banks</c> catalogue (no RLS). Business
/// accounts reference a bank by <c>bank_id</c>, so creating an account must first
/// confirm the bank exists and is active.
/// </summary>
public interface IBankRepository
{
    /// <summary>Returns the active bank with the given id, or <c>null</c> when unknown/inactive.</summary>
    Task<BankRecord?> FindActiveAsync(short bankId, CancellationToken cancellationToken = default);
}

/// <summary>A bank as needed by the business-account module.</summary>
public sealed record BankRecord(short Id, string Code, string Name, bool IsActive);
