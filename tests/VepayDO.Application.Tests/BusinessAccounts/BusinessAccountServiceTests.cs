using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.BusinessAccounts;

/// <summary>
/// Unit tests for <see cref="BusinessAccountService"/> (UC-SET-03). They pin the
/// last4-only shape, the bank check, the per-tenant duplicate guard, and the
/// one-audit-event invariant.
/// </summary>
public sealed class BusinessAccountServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task List_returns_the_tenants_accounts()
    {
        var harness = new Harness();
        harness.Accounts.Items.Add(Sample());

        var result = await harness.Service.ListAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Last4.Should().Be("1234");
    }

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Get_of_an_unknown_account_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_stores_only_the_last_four_digits()
    {
        var harness = new Harness();

        var view = await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", " 4321 ", "dop"),
            Meta(),
            CancellationToken.None);

        view.Last4.Should().Be("4321");
        view.Currency.Should().Be("DOP");
        view.BankName.Should().Be("Banco Test");
        harness.Accounts.Added!.Last4.Should().Be("4321");
        view.Last4.Should().NotContain("-");
    }

    [Fact]
    public async Task Create_rejects_an_unknown_bank()
    {
        var harness = new Harness();
        harness.Banks.Known = null;

        var act = async () => await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(99, "Principal", "4321", "DOP"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_rejects_a_malformed_last4()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", "12ab", "DOP"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_bank_last4_currency()
    {
        var harness = new Harness();
        harness.Accounts.Duplicate = true;

        var act = async () => await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", "4321", "DOP"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_writes_exactly_one_audit_event()
    {
        var harness = new Harness();

        await harness.Service.CreateAsync(
            new CreateBusinessAccountInput(1, "Principal", "4321", "DOP"),
            Meta(),
            CancellationToken.None);

        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Entity.Should().Be("business_account");
        harness.Audit.Events[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task Update_deactivates_and_audits()
    {
        var harness = new Harness();
        var account = Sample();
        harness.Accounts.Items.Add(account);

        var view = await harness.Service.UpdateAsync(
            account.Id,
            new UpdateBusinessAccountInput(null, IsActive: false),
            Meta(),
            CancellationToken.None);

        view.IsActive.Should().BeFalse();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("update");
    }

    [Fact]
    public async Task Update_of_an_unknown_account_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateBusinessAccountInput("X", null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static RequestMeta Meta() => new(null, null, null);

    private static BusinessAccountRecord Sample() =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            BankId: 1,
            BankName: "Banco Test",
            Label: "Principal",
            Last4: "1234",
            Currency: "DOP",
            IsActive: true,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Accounts = new FakeBusinessAccountRepository();
            Banks = new FakeBankRepository();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, UserId, DbRole.Owner);
            Service = new BusinessAccountService(
                Accounts,
                Banks,
                Audit,
                new FixedClock(DateTimeOffset.Parse("2026-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
                new FakeTenantContextAccessor(ctx));
        }

        public FakeBusinessAccountRepository Accounts { get; }

        public FakeBankRepository Banks { get; }

        public RecordingAuditWriter Audit { get; }

        public BusinessAccountService Service { get; }
    }

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
