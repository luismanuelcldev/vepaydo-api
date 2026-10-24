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

/// <summary>
/// Unit tests for <see cref="ChargeService"/> (UC-DATA-03 to 06): validation,
/// code uniqueness and generation, voiding rules, the duplicate prototype path,
/// CSV import reporting, and the one-audit-event-per-mutation invariant.
/// </summary>
public sealed class ChargeServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ChargeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Create_generates_a_code_when_none_is_supplied()
    {
        var harness = new Harness();

        var view = await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Servicio de marzo", 1500m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        view.Code.Should().Be("SER-000042");
        view.Status.Should().Be("OPEN");
        view.Balance.Should().Be(1500m);
        view.AmountApplied.Should().Be(0m);
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_explicit_code()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, "fact-001", "Otro", 10m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_rejects_a_non_positive_amount()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Malo", 0m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Create_rejects_a_due_date_before_the_issue_date()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Malo", 10m, "DOP", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 1)),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Create_rejects_an_unsupported_currency()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Malo", 10m, "EUR", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Update_rejects_a_voided_charge()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001", status: "VOIDED"));

        var act = async () => await harness.Service.UpdateAsync(
            ChargeId,
            new UpdateChargeInput(null, null, "Nuevo", null, null, null, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_applies_only_the_supplied_fields()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var view = await harness.Service.UpdateAsync(
            ChargeId,
            new UpdateChargeInput(null, null, "Descripción nueva", 2500m, null, null, null),
            Meta(),
            CancellationToken.None);

        view.Description.Should().Be("Descripción nueva");
        view.Amount.Should().Be(2500m);
        view.Code.Should().Be("FACT-001");
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("update");
    }

    [Fact]
    public async Task Void_records_the_reason_and_audits()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var view = await harness.Service.VoidAsync(
            ChargeId,
            new VoidChargeInput("Creada por error"),
            Meta(),
            CancellationToken.None);

        view.Status.Should().Be("VOIDED");
        view.VoidReason.Should().Be("Creada por error");
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("void");
    }

    [Fact]
    public async Task Void_rejects_an_already_voided_charge()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001", status: "VOIDED"));

        var act = async () => await harness.Service.VoidAsync(
            ChargeId,
            new VoidChargeInput("otra"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Void_requires_a_reason()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var act = async () => await harness.Service.VoidAsync(
            ChargeId,
            new VoidChargeInput("   "),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Duplicate_creates_an_open_copy_with_a_fresh_code()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001", status: "PARTIAL", applied: 500m));

        var view = await harness.Service.DuplicateAsync(
            ChargeId,
            new DuplicateChargeInput(null),
            Meta(),
            CancellationToken.None);

        view.Id.Should().NotBe(ChargeId);
        view.Status.Should().Be("OPEN");
        view.AmountApplied.Should().Be(0m);
        view.Code.Should().Be("SER-000042");
        view.Amount.Should().Be(1500m);
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("duplicate");
    }

    [Fact]
    public async Task Import_reports_invalid_rows_and_inserts_the_valid_ones()
    {
        var harness = new Harness();
        var rows = new List<ChargeImportRow>
        {
            Row(1, "Buena", 100m),
            Row(2, "Mala", 0m),
            Row(3, "Otra", 200m),
        };

        var result = await harness.Service.ImportAsync(rows, allOrNothing: false, Meta(), CancellationToken.None);

        result.Imported.Should().Be(2);
        result.Skipped.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.RowNumber.Should().Be(2);
        harness.Repo.Added.Should().HaveCount(2);
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("import");
    }

    [Fact]
    public async Task Import_all_or_nothing_writes_nothing_on_any_error()
    {
        var harness = new Harness();
        var rows = new List<ChargeImportRow>
        {
            Row(1, "Buena", 100m),
            Row(2, "Mala", -5m),
        };

        var result = await harness.Service.ImportAsync(rows, allOrNothing: true, Meta(), CancellationToken.None);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(2);
        result.Errors.Should().ContainSingle();
        harness.Repo.Added.Should().BeEmpty();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("import");
    }

    [Fact]
    public async Task Create_retries_with_a_new_code_when_the_unique_index_wins_the_race()
    {
        var harness = new Harness();
        harness.Repo.ConflictsBeforeSuccess = 1;

        var view = await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Servicio de marzo", 1500m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        view.Code.Should().Be("SER-000043");
        harness.Repo.Added.Should().ContainSingle();
    }

    [Fact]
    public async Task List_with_an_unknown_status_fails_closed()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.ListAsync(new ChargeQuery("NOPE", null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

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

    private sealed class FakeChargeRepository : IChargeRepository
    {
        private readonly List<ChargeRecord> _items = [];

        public List<NewCharge> Added { get; } = [];

        /// <summary>Number of inserts that fail with a unique-code conflict before one succeeds.</summary>
        public int ConflictsBeforeSuccess { get; set; }

        public void Seed(ChargeRecord record) => _items.Add(record);

        public Task<IReadOnlyList<ChargeRecord>> ListAsync(
            Guid tenantId,
            ChargeQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ChargeRecord>>(_items);

        public Task<ChargeRecord?> FindAsync(
            Guid tenantId,
            Guid chargeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(c => c.Id == chargeId));

        public Task<bool> CodeExistsAsync(
            Guid tenantId,
            string code,
            Guid? excludingChargeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Any(c => c.Code == code && c.Id != excludingChargeId));

        public Task<Guid> AddAsync(
            Guid tenantId,
            NewCharge charge,
            CancellationToken cancellationToken = default)
        {
            if (ConflictsBeforeSuccess > 0)
            {
                ConflictsBeforeSuccess--;
                return Task.FromException<Guid>(new ConflictException("Charge code already exists in this tenant"));
            }

            var id = Guid.NewGuid();
            Added.Add(charge);
            _items.Add(new ChargeRecord(
                id,
                charge.CustomerId,
                charge.Code,
                charge.Description,
                charge.Amount,
                charge.Currency,
                charge.IssuedOn,
                charge.DueOn,
                "OPEN",
                0m,
                charge.Amount,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch));
            return Task.FromResult(id);
        }

        public Task UpdateAsync(
            Guid tenantId,
            Guid chargeId,
            UpdateChargeFields fields,
            CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(c => c.Id == chargeId);
            if (index >= 0)
            {
                _items[index] = _items[index] with
                {
                    CustomerId = fields.CustomerId,
                    Code = fields.Code,
                    Description = fields.Description,
                    Amount = fields.Amount,
                    Currency = fields.Currency,
                    IssuedOn = fields.IssuedOn,
                    DueOn = fields.DueOn,
                    Balance = fields.Amount - _items[index].AmountApplied,
                };
            }

            return Task.CompletedTask;
        }

        public Task VoidAsync(
            Guid tenantId,
            Guid chargeId,
            string reason,
            Guid? voidedBy,
            CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(c => c.Id == chargeId);
            if (index >= 0)
            {
                _items[index] = _items[index] with
                {
                    Status = "VOIDED",
                    VoidReason = reason,
                    VoidedAt = DateTimeOffset.UnixEpoch,
                };
            }

            return Task.CompletedTask;
        }

        public Task AddManyAsync(
            Guid tenantId,
            IReadOnlyList<NewCharge> charges,
            CancellationToken cancellationToken = default)
        {
            foreach (var charge in charges)
            {
                Added.Add(charge);
                _items.Add(new ChargeRecord(
                    Guid.NewGuid(),
                    charge.CustomerId,
                    charge.Code,
                    charge.Description,
                    charge.Amount,
                    charge.Currency,
                    charge.IssuedOn,
                    charge.DueOn,
                    "OPEN",
                    0m,
                    charge.Amount,
                    null,
                    null,
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch));
            }

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

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
