using VepayDO.Application.Customers;
using VepayDO.Application.Customers.Ports;

namespace VepayDO.Application.Tests.Customers;

public sealed partial class CustomerServiceTests
{
    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly List<CustomerRecord> _items = [];
        public Dictionary<Guid, List<string>> Aliases { get; } = [];

        public void Add(CustomerRecord record) { _items.Add(record); Aliases.TryAdd(record.Id, []); }
        public Task<IReadOnlyList<CustomerRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerRecord>>(_items);
        public Task<CustomerRecord?> FindAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(c => c.Id == customerId));
        public Task<IReadOnlyList<string>> ListAliasesAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Aliases.TryGetValue(customerId, out var list) ? list : []);

        public Task<CustomerAliasRecord?> FindAliasAsync(Guid tenantId, string aliasNormalized, CancellationToken cancellationToken = default)
        {
            foreach (var (customerId, list) in Aliases)
            {
                if (list.Contains(aliasNormalized)) return Task.FromResult<CustomerAliasRecord?>(new CustomerAliasRecord(Guid.NewGuid(), customerId, aliasNormalized));
            }
            return Task.FromResult<CustomerAliasRecord?>(null);
        }

        public Task<Guid> AddAsync(Guid tenantId, CustomerRecord customer, CancellationToken cancellationToken = default)
        {
            Add(customer);
            return Task.FromResult(customer.Id);
        }

        public Task UpdateAsync(Guid tenantId, Guid customerId, string name, string nameNormalized, string? phone, string? notes, bool isActive, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(c => c.Id == customerId);
            if (index >= 0) _items[index] = _items[index] with { Name = name, NameNormalized = nameNormalized, Phone = phone, Notes = notes, IsActive = isActive };
            return Task.CompletedTask;
        }

        public Task AddAliasAsync(Guid tenantId, Guid customerId, string aliasNormalized, CancellationToken cancellationToken = default)
        {
            Aliases.TryAdd(customerId, []);
            if (!Aliases[customerId].Contains(aliasNormalized)) Aliases[customerId].Add(aliasNormalized);
            return Task.CompletedTask;
        }

        public Task RemoveAliasAsync(Guid tenantId, string aliasNormalized, CancellationToken cancellationToken = default)
        {
            foreach (var list in Aliases.Values) list.Remove(aliasNormalized);
            return Task.CompletedTask;
        }
    }
}
