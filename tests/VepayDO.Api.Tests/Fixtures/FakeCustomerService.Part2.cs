using VepayDO.Application.Auth;
using VepayDO.Application.Customers;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeCustomerService
{

    public Task<CustomerView> UpdateAsync(
        Guid customerId,
        UpdateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<CustomerView>(Error);
        }

        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new CustomerView(
            customerId,
            input.Name ?? "Customer",
            (input.Name ?? "Customer").ToLowerInvariant(),
            input.Phone,
            input.Notes,
            input.IsActive ?? true,
            Aliases: [],
            now,
            now));
    }

    public Task<CustomerView> AddAliasAsync(
        Guid customerId,
        AddAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<CustomerView>(Error);
        }

        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new CustomerView(
            customerId,
            "Customer",
            "customer",
            null,
            null,
            IsActive: true,
            Aliases: [input.Alias.ToLowerInvariant()],
            now,
            now));
    }
}
