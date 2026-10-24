using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Customers;
using VepayDO.Application.Customers.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Customers;

public sealed partial class CustomerServiceTests
{

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
