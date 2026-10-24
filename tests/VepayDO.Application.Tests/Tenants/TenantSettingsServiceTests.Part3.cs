using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tests.Tenants;

public sealed partial class TenantSettingsServiceTests
{

    [Fact]
    public async Task Update_writes_exactly_one_audit_event_with_before_and_after()
    {
        var harness = new Harness();

        await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(45, null, null, false, null),
            Meta(),
            CancellationToken.None);

        harness.Audit.Events.Should().ContainSingle();
        var draft = harness.Audit.Events[0];
        draft.Entity.Should().Be("tenant_settings");
        draft.Action.Should().Be("update");
        draft.TenantId.Should().Be(TenantId);
        draft.Before.Should().Contain("\"DateWindowDays\":7");
        draft.After.Should().Contain("\"DateWindowDays\":45");
    }

    private static RequestMeta Meta() => new(null, null, null);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Settings = new FakeTenantSettingsRepository();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, UserId, DbRole.Owner);
            Service = new TenantSettingsService(Settings, Audit, new FakeTenantContextAccessor(ctx));
        }

        public FakeTenantSettingsRepository Settings { get; }

        public RecordingAuditWriter Audit { get; }

        public TenantSettingsService Service { get; }

        public bool RowMissing
        {
            init => Settings.RowMissing = value;
        }
    }

    private sealed class FakeTenantSettingsRepository : ITenantSettingsRepository
    {
        public TenantSettingsValues? Stored { get; set; } = TenantSettingsValues.Default.FromPrototype();

        public bool RowMissing { get; set; }

        public Task<TenantSettingsValues?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RowMissing ? null : Stored);

        public Task UpdateAsync(Guid tenantId, TenantSettingsValues values, CancellationToken cancellationToken = default)
        {
            Stored = values;
            return Task.CompletedTask;
        }
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
}
