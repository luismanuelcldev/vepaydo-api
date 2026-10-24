using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.BusinessAccounts;

public sealed partial class BusinessAccountServiceTests
{

    private sealed class FakeBusinessAccountRepository : IBusinessAccountRepository
    {
        public List<BusinessAccountRecord> Items { get; } = [];

        public bool Duplicate { get; set; }

        public NewBusinessAccount? Added { get; private set; }

        public Task<IReadOnlyList<BusinessAccountRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BusinessAccountRecord>>(Items);

        public Task<BusinessAccountRecord?> FindAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(a => a.Id == accountId));

        public Task<bool> ExistsDuplicateAsync(
            Guid tenantId,
            short bankId,
            string last4,
            string currency,
            Guid? excludingAccountId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Duplicate);

        public Task<Guid> AddAsync(Guid tenantId, NewBusinessAccount account, CancellationToken cancellationToken = default)
        {
            Added = account;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task UpdateAsync(
            Guid tenantId,
            Guid accountId,
            string label,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(a => a.Id == accountId);
            if (index >= 0)
            {
                Items[index] = Items[index] with { Label = label, IsActive = isActive };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeBankRepository : IBankRepository
    {
        public BankRecord? Known { get; set; } = new(1, "TEST", "Banco Test", true);

        public Task<BankRecord?> FindActiveAsync(short bankId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Known is not null && Known.Id == bankId ? Known : null);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEventDraft> Events { get; } = [];

        public Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default)
        {
            Events.Add(draft);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantContextAccessor(TenantContext? context) : ITenantContextAccessor
    {
        public TenantContext? Current { get; } = context;
    }
}
