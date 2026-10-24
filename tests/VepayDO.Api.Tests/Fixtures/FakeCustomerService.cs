using VepayDO.Application.Auth;
using VepayDO.Application.Customers;

namespace VepayDO.Api.Tests.Fixtures;

public sealed partial class FakeCustomerService : ICustomerService
{
    public IReadOnlyList<CustomerView> Customers { get; set; } = [];

    public Exception? Error { get; set; }

    public MoveAliasInput? LastMove { get; private set; }

    public void Reset()
    {
        Customers = [];
        Error = null;
        LastMove = null;
    }

    public Task<IReadOnlyList<CustomerView>> ListAsync(CancellationToken cancellationToken = default) =>
        Error is not null
            ? Task.FromException<IReadOnlyList<CustomerView>>(Error)
            : Task.FromResult(Customers);

    public Task<CustomerView> GetAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            return Task.FromException<CustomerView>(Error);
        }

        var found = Customers.FirstOrDefault(c => c.Id == customerId);
        return found is null
            ? Task.FromException<CustomerView>(new VepayDO.Domain.Errors.NotFoundException("Customer not found"))
            : Task.FromResult(found);
    }

    public Task<CustomerView> CreateAsync(
        CreateCustomerInput input,
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
            Guid.NewGuid(),
            input.Name,
            input.Name.ToLowerInvariant(),
            input.Phone,
            input.Notes,
            IsActive: true,
            Aliases: [],
            now,
            now));
    }
}
