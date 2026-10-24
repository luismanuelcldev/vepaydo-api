using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Domain.Customers;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Customers;

public sealed partial class CustomerService
{
    public async Task<CustomerView> AddAliasAsync(Guid customerId, AddAliasInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var customer = await customers.FindAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false)
                       ?? throw new NotFoundException("Customer not found");

        var alias = CustomerName.Parse(input.Alias);
        var existing = await customers.FindAliasAsync(tenantId, alias.Normalized, cancellationToken).ConfigureAwait(false);

        if (existing is not null && existing.CustomerId == customerId)
        {
            var own = await customers.ListAliasesAsync(tenantId, customerId, cancellationToken).ConfigureAwait(false);
            return ToView(customer, own);
        }

        if (existing is not null) throw new ConflictException("This sender is already associated with another customer");

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

    public async Task<CustomerView> MoveAliasAsync(Guid customerId, MoveAliasInput input, RequestMeta meta, CancellationToken cancellationToken = default)
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

        if (existing is null || existing.CustomerId != input.FromCustomerId)
            throw new ConflictException("The alias is not currently owned by the source customer");

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
}
