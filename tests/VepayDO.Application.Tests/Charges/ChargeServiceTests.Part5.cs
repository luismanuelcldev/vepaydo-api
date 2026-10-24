using System.Globalization;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Charges;

public sealed partial class ChargeServiceTests
{

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(new ChargeQuery(null, null), CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    private static ChargeImportRow Row(int n, string description, decimal amount) =>
        new(n, null, null, description, amount, "DOP", new DateOnly(2026, 3, 1), null);

    private static RequestMeta Meta() => new(null, null, null);

    private static ChargeRecord Sample(
        Guid id,
        string code,
        string status = "OPEN",
        decimal applied = 0m) =>
        new(
            id,
            CustomerId,
            code,
            "Servicio de marzo",
            1500m,
            "DOP",
            new DateOnly(2026, 3, 1),
            null,
            status,
            applied,
            1500m - applied,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Repo = new FakeChargeRepository();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, UserId, DbRole.Owner);
            Service = new ChargeService(
                Repo,
                new SequentialCodeSource(),
                Audit,
                new FixedClock(DateTimeOffset.Parse("2026-03-01T12:00:00Z", CultureInfo.InvariantCulture)),
                new FakeTenantContextAccessor(ctx));
        }

        public FakeChargeRepository Repo { get; }

        public RecordingAuditWriter Audit { get; }

        public ChargeService Service { get; }
    }

    private sealed class SequentialCodeSource : IChargeCodeSource
    {
        private int _next = 42;

        public int NextValue(int maxExclusive) => _next++;
    }
}
