using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Customers.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Customers;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Text;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Customers;

/// <summary>
/// Customer and alias use cases (UC-DATA-01, UC-DATA-02). Names are normalized
/// through <see cref="TextNormalizer"/> so the stored <c>name_normalized</c> and
/// the <c>SENDER</c> matching signal agree. Alias uniqueness per tenant is
/// checked here (friendly domain error) and enforced by the unique index as the
/// backstop, so a raw constraint violation is never surfaced. Every mutation is
/// audited in the same transaction.
/// </summary>
public sealed class CustomerService(
    ICustomerRepository customers,
    IAuditWriter audit,
    IClock clock,
    ITenantContextAccessor tenantContext) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerView>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var records = await customers.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var views = new List<CustomerView>(records.Count);
        foreach (var record in records)
        {
            var aliases = await customers.ListAliasesAsync(tenantId, record.Id, cancellationToken).ConfigureAwait(false);
            views.Add(ToView(record, aliases));
        }

        return views;
    }

    public async Task<CustomerView> GetAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var record = await customers.FindAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Customer not found");

        var aliases = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
        return ToView(record, aliases);
    }

    public async Task<CustomerView> CreateAsync(
        CreateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var name = CustomerName.Parse(input.Name);
        var now = clock.UtcNow;
        var record = new CustomerRecord(
            Guid.NewGuid(),
            name.Display,
            name.Normalized,
            NormalizeOptional(input.Phone),
            NormalizeOptional(input.Notes),
            IsActive: true,
            now,
            now);

        var id = await customers.AddAsync(tenantId, record, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("customer")
                .EntityId(id)
                .Action("create")
                .After(new { id, name = name.Display, nameNormalized = name.Normalized })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        return ToView(record with { Id = id }, aliases: []);
    }

    public async Task<CustomerView> UpdateAsync(
        Guid customerId,
        UpdateCustomerInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var current = await customers.FindAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Customer not found");

        var parsedName = input.Name is null ? null : CustomerName.Parse(input.Name);
        var newName = parsedName?.Display ?? current.Name;
        var newNormalized = parsedName?.Normalized ?? current.NameNormalized;
        var newPhone = input.Phone is null ? current.Phone : NormalizeOptional(input.Phone);
        var newNotes = input.Notes is null ? current.Notes : NormalizeOptional(input.Notes);
        var newActive = input.IsActive ?? current.IsActive;

        await customers
            .UpdateAsync(tenantId, customerId, newName, newNormalized, newPhone, newNotes, newActive, cancellationToken)
            .ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("customer")
                .EntityId(customerId)
                .Action("update")
                .Before(new { name = current.Name, phone = current.Phone, notes = current.Notes, isActive = current.IsActive })
                .After(new { name = newName, phone = newPhone, notes = newNotes, isActive = newActive })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        var aliases = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);

        return ToView(
            current with
            {
                Name = newName,
                NameNormalized = newNormalized,
                Phone = newPhone,
                Notes = newNotes,
                IsActive = newActive,
                UpdatedAt = clock.UtcNow,
            },
            aliases);
    }

    public async Task<CustomerView> AddAliasAsync(
        Guid customerId,
        AddAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var customer = await customers.FindAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false)
                       ?? throw new NotFoundException("Customer not found");

        var alias = CustomerName.Parse(input.Alias);
        var existing = await customers.FindAliasAsync(tenantId, alias.Normalized, cancellationToken).ConfigureAwait(false);

        // Re-adding the customer's own alias is a no-op (idempotent).
        if (existing is not null && existing.CustomerId == customerId)
        {
            var own = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
            return ToView(customer, own);
        }

        // The alias already identifies a different customer: surface a controlled
        // conflict (UC-DATA-02 alternate flow) instead of a raw constraint error.
        if (existing is not null)
        {
            throw new ConflictException("This sender is already associated with another customer");
        }

        await customers.AddAliasAsync(tenantId, customerId, alias.Normalized, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("customer_alias")
                .EntityId(customerId)
                .Action("add")
                .After(new { customerId, aliasNormalized = alias.Normalized })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        var aliases = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
        return ToView(customer, aliases);
    }

    public async Task<CustomerView> MoveAliasAsync(
        Guid customerId,
        MoveAliasInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var target = await customers.FindAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Customer not found");

        var from = await customers.FindAsync(tenantId, input.FromCustomerId, cancellationToken).ConfigureAwait(false)
                   ?? throw new NotFoundException("Source customer not found");

        var alias = CustomerName.Parse(input.Alias);
        var existing = await customers.FindAliasAsync(tenantId, alias.Normalized, cancellationToken).ConfigureAwait(false);

        // Moving only makes sense when the alias currently belongs to the source
        // customer; anything else is a conflict the caller can act on.
        if (existing is null || existing.CustomerId != input.FromCustomerId)
        {
            throw new ConflictException("The alias is not currently owned by the source customer");
        }

        if (input.FromCustomerId == customerId)
        {
            var unchanged = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
            return ToView(target, unchanged);
        }

        await customers.RemoveAliasAsync(tenantId, alias.Normalized, cancellationToken).ConfigureAwait(false);
        await customers.AddAliasAsync(tenantId, customerId, alias.Normalized, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("customer_alias")
                .EntityId(customerId)
                .Action("move")
                .Before(new { customerId = from.Id, aliasNormalized = alias.Normalized })
                .After(new { customerId, aliasNormalized = alias.Normalized })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        var aliases = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
        return ToView(target, aliases);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CustomerView ToView(CustomerRecord record, IReadOnlyList<string> aliases) =>
        new(
            record.Id,
            record.Name,
            record.NameNormalized,
            record.Phone,
            record.Notes,
            record.IsActive,
            aliases,
            record.CreatedAt,
            record.UpdatedAt);

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        return tenantId.Value;
    }
}
