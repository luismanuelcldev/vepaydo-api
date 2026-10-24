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

    private static CustomerRecord Sample(Guid id, string name) =>
        new(id, name, name.ToLowerInvariant(), null, null, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Repo = new FakeCustomerRepository();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, UserId, DbRole.Owner);
            Service = new CustomerService(
                Repo,
                Audit,
                new FixedClock(DateTimeOffset.Parse("2026-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
                new FakeTenantContextAccessor(ctx));
        }

        public FakeCustomerRepository Repo { get; }

        public RecordingAuditWriter Audit { get; }

        public CustomerService Service { get; }
    }
}
