using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tests.Tenants;

/// <summary>
/// Unit tests for <see cref="TenantSettingsService"/> (UC-SET-04). They pin the
/// tenant scoping, the domain validation, and the invariant that every update
/// writes exactly one audit event.
/// </summary>
public sealed class TenantSettingsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Get_returns_the_stored_values()
    {
        var harness = new Harness();

        var view = await harness.Service.GetAsync(CancellationToken.None);

        view.DateWindowDays.Should().Be(7);
        view.AmountTolerance.Should().Be(0m);
        view.VerifierAmountCap.Should().BeNull();
        view.ImageRetentionMonths.Should().Be(24);
    }

    [Fact]
    public async Task Get_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.GetAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Get_with_a_missing_settings_row_is_not_found()
    {
        var harness = new Harness { RowMissing = true };

        var act = async () => await harness.Service.GetAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_applies_a_partial_change_and_keeps_the_rest()
    {
        var harness = new Harness();

        var view = await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(
                DateWindowDays: 30,
                AmountTolerance: null,
                VerifierAmountCap: null,
                ClearVerifierAmountCap: false,
                ImageRetentionMonths: null),
            Meta(),
            CancellationToken.None);

        view.DateWindowDays.Should().Be(30);
        view.AmountTolerance.Should().Be(0m);
        view.VerifierAmountCap.Should().BeNull();
        view.ImageRetentionMonths.Should().Be(24);
        harness.Settings.Stored!.DateWindowDays.Should().Be(30);
    }

    [Fact]
    public async Task Update_can_clear_the_verifier_cap()
    {
        var harness = new Harness();
        harness.Settings.Stored = TenantSettingsValues.Default.FromPrototype() with { VerifierAmountCap = 5000m };

        var view = await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, null, null, ClearVerifierAmountCap: true, null),
            Meta(),
            CancellationToken.None);

        view.VerifierAmountCap.Should().BeNull();
    }

    [Fact]
    public async Task Update_can_set_the_verifier_cap()
    {
        var harness = new Harness();

        var view = await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, null, 2500m, ClearVerifierAmountCap: false, null),
            Meta(),
            CancellationToken.None);

        view.VerifierAmountCap.Should().Be(2500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Update_rejects_an_out_of_range_window(short days)
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(days, null, null, false, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_rejects_a_negative_tolerance()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, -1m, null, false, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Update_rejects_a_non_positive_cap()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, null, 0m, false, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

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

    private sealed class FakeTenantContextAccessor(TenantContext? context) : ITenantContextAccessor
    {
        public TenantContext? Current { get; } = context;
    }
}