using VepayDO.Application.Customers;
using VepayDO.Contracts.Customers;

namespace VepayDO.Api.Controllers.Customers;

/// <summary>
/// Maps the customer application results onto the wire contracts, keeping
/// <c>VepayDO.Contracts</c> free of any Application dependency (AGENTS.md section 6).
/// </summary>
internal static class CustomerMappers
{
    public static CustomerResponse ToResponse(this CustomerView customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.NameNormalized,
            customer.Phone,
            customer.Notes,
            customer.IsActive,
            customer.Aliases,
            customer.CreatedAt,
            customer.UpdatedAt);
    }

    public static IReadOnlyList<CustomerResponse> ToResponse(this IReadOnlyList<CustomerView> customers)
    {
        ArgumentNullException.ThrowIfNull(customers);

        return customers.Select(ToResponse).ToList();
    }
}
