using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Customers.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Customers;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Customers;

public sealed partial class CustomerService(
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

    public async Task<CustomerView> CreateAsync(CreateCustomerInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var name = CustomerName.Parse(input.Name);
        var now = clock.UtcNow;
        var record = new CustomerRecord(Guid.NewGuid(), name.Display, name.Normalized, NormalizeOptional(input.Phone), NormalizeOptional(input.Notes), IsActive: true, now, now);
        var id = await customers.AddAsync(tenantId, record, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(tenantContext.Current?.UserId).Entity("customer").EntityId(id).Action("create").After(new { id, name = name.Display, nameNormalized = name.Normalized }).Ip(meta.Ip).Build(), cancellationToken).ConfigureAwait(false);

        return ToView(record with { Id = id }, aliases: []);
    }

    public async Task<CustomerView> UpdateAsync(Guid customerId, UpdateCustomerInput input, RequestMeta meta, CancellationToken cancellationToken = default)
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

        await customers.UpdateAsync(tenantId, customerId, newName, newNormalized, newPhone, newNotes, newActive, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(new AuditEventBuilder().TenantId(tenantId).UserId(tenantContext.Current?.UserId).Entity("customer").EntityId(customerId).Action("update").Before(new { name = current.Name, phone = current.Phone, notes = current.Notes, isActive = current.IsActive }).After(new { name = newName, phone = newPhone, notes = newNotes, isActive = newActive }).Ip(meta.Ip).Build(), cancellationToken).ConfigureAwait(false);

        var aliases = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
        return ToView(current with { Name = newName, NameNormalized = newNormalized, Phone = newPhone, Notes = newNotes, IsActive = newActive, UpdatedAt = clock.UtcNow }, aliases);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CustomerView ToView(CustomerRecord record, IReadOnlyList<string> aliases) =>
        new(record.Id, record.Name, record.NameNormalized, record.Phone, record.Notes, record.IsActive, aliases, record.CreatedAt, record.UpdatedAt);
    private Guid RequireTenantId() => tenantContext.Current?.TenantId is { } id && id != Guid.Empty ? id : throw new MissingTenantContextException();
}
