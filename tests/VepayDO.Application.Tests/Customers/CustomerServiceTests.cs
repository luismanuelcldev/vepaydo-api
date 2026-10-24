using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Customers;
using VepayDO.Application.Customers.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Customers;

/// <summary>
/// Unit tests for <see cref="CustomerService"/> (UC-DATA-01, UC-DATA-02). They pin
/// name normalization, the idempotent self-alias add, the cross-customer alias
/// conflict, the OWNER-only move (enforced upstream by the route policy), and the
/// one-audit-event invariant.
/// </summary>
public sealed class CustomerServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherCustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task List_returns_customers_with_their_aliases()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María Pérez"));
        harness.Repo.Aliases[CustomerId] = ["maria perez"];

        var result = await harness.Service.ListAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("María Pérez");
        result[0].Aliases.Should().ContainSingle().Which.Should().Be("maria perez");
    }

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Create_normalizes_the_name()
    {
        var harness = new Harness();

        var view = await harness.Service.CreateAsync(
            new CreateCustomerInput("  MARÍA   Pérez ", null, "nota"),
            Meta(),
            CancellationToken.None);

        view.Name.Should().Be("MARÍA   Pérez");
        view.NameNormalized.Should().Be("maria perez");
        view.Notes.Should().Be("nota");
        view.IsActive.Should().BeTrue();
        harness.Audit.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_rejects_an_empty_name()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateCustomerInput("   ", null, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Update_can_rename_and_deactivate()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "Old Name"));

        var view = await harness.Service.UpdateAsync(
            CustomerId,
            new UpdateCustomerInput("Nuño Álvarez", null, null, IsActive: false),
            Meta(),
            CancellationToken.None);

        view.Name.Should().Be("Nuño Álvarez");
        view.NameNormalized.Should().Be("nuno alvarez");
        view.IsActive.Should().BeFalse();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("update");
    }

    [Fact]
    public async Task Add_alias_is_idempotent_for_the_same_customer()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Aliases[CustomerId] = ["maria p"];

        var view = await harness.Service.AddAliasAsync(
            CustomerId,
            new AddAliasInput("  María P "),
            Meta(),
            CancellationToken.None);

        view.Aliases.Should().ContainSingle();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_alias_of_another_customer_is_a_conflict()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Add(Sample(OtherCustomerId, "Otro"));
        harness.Repo.Aliases[OtherCustomerId] = ["maria p"];

        var act = async () => await harness.Service.AddAliasAsync(
            CustomerId,
            new AddAliasInput("Maria P"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_alias_normalizes_and_audits()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));

        await harness.Service.AddAliasAsync(
            CustomerId,
            new AddAliasInput("  José  PeReZ "),
            Meta(),
            CancellationToken.None);

        harness.Repo.Aliases[CustomerId].Should().ContainSingle().Which.Should().Be("jose perez");
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Entity.Should().Be("customer_alias");
        harness.Audit.Events[0].Action.Should().Be("add");
    }

    [Fact]
    public async Task Add_alias_to_an_unknown_customer_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.AddAliasAsync(
            Guid.NewGuid(),
            new AddAliasInput("x"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Move_alias_moves_it_and_audits_both_customers()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Add(Sample(OtherCustomerId, "Otro"));
        harness.Repo.Aliases[OtherCustomerId] = ["maria p"];

        var view = await harness.Service.MoveAliasAsync(
            CustomerId,
            new MoveAliasInput(OtherCustomerId, "Maria P"),
            Meta(),
            CancellationToken.None);

        view.Aliases.Should().ContainSingle().Which.Should().Be("maria p");
        harness.Repo.Aliases[OtherCustomerId].Should().BeEmpty();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("move");
    }

    [Fact]
    public async Task Move_alias_rejects_a_source_that_does_not_own_it()
    {
        var harness = new Harness();
        harness.Repo.Add(Sample(CustomerId, "María"));
        harness.Repo.Add(Sample(OtherCustomerId, "Otro"));

        var act = async () => await harness.Service.MoveAliasAsync(
            CustomerId,
            new MoveAliasInput(OtherCustomerId, "ghost"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    private static RequestMeta Meta() => new(null, null, null);

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

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly List<CustomerRecord> _items = [];

        public Dictionary<Guid, List<string>> Aliases { get; } = [];

        public void Add(CustomerRecord record)
        {
            _items.Add(record);
            Aliases.TryAdd(record.Id, []);
        }

        public Task<IReadOnlyList<CustomerRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerRecord>>(_items);

        public Task<CustomerRecord?> FindAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(c => c.Id == customerId));

        public Task<IReadOnlyList<string>> ListAliasesAsync(
            Guid tenantId,
            Guid customerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Aliases.TryGetValue(customerId, out var list) ? list : []);

        public Task<CustomerAliasRecord?> FindAliasAsync(
            Guid tenantId,
            string aliasNormalized,
            CancellationToken cancellationToken = default)
        {
            foreach (var (customerId, list) in Aliases)
            {
                if (list.Contains(aliasNormalized))
                {
                    return Task.FromResult<CustomerAliasRecord?>(
                        new CustomerAliasRecord(Guid.NewGuid(), customerId, aliasNormalized));
                }
            }

            return Task.FromResult<CustomerAliasRecord?>(null);
        }

        public Task<Guid> AddAsync(Guid tenantId, CustomerRecord customer, CancellationToken cancellationToken = default)
        {
            Add(customer);
            return Task.FromResult(customer.Id);
        }

        public Task UpdateAsync(
            Guid tenantId,
            Guid customerId,
            string name,
            string nameNormalized,
            string? phone,
            string? notes,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(c => c.Id == customerId);
            if (index >= 0)
            {
                _items[index] = _items[index] with
                {
                    Name = name,
                    NameNormalized = nameNormalized,
                    Phone = phone,
                    Notes = notes,
                    IsActive = isActive,
                };
            }

            return Task.CompletedTask;
        }

        public Task AddAliasAsync(
            Guid tenantId,
            Guid customerId,
            string aliasNormalized,
            CancellationToken cancellationToken = default)
        {
            Aliases.TryAdd(customerId, []);
            if (!Aliases[customerId].Contains(aliasNormalized))
            {
                Aliases[customerId].Add(aliasNormalized);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAliasAsync(
            Guid tenantId,
            string aliasNormalized,
            CancellationToken cancellationToken = default)
        {
            foreach (var list in Aliases.Values)
            {
                list.Remove(aliasNormalized);
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
}
