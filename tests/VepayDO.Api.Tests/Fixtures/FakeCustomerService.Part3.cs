using VepayDO.Application.Auth;
using VepayDO.Application.Customers;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeCustomerService
{

    public Task<CustomerView> MoveAliasAsync(
        Guid customerId,
        MoveAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        LastMove = input;

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
