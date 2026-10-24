using VepayDO.Application.Auth;

namespace VepayDO.Application.Customers;

public interface ICustomerService
{

    Task<IReadOnlyList<CustomerView>> ListAsync(CancellationToken cancellationToken = default);

    Task<CustomerView> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<CustomerView> CreateAsync(
        CreateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<CustomerView> UpdateAsync(
        Guid customerId,
        UpdateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<CustomerView> AddAliasAsync(
        Guid customerId,
        AddAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<CustomerView> MoveAliasAsync(
        Guid customerId,
        MoveAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);
}
