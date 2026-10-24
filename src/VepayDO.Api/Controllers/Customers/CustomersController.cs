using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Customers;
using VepayDO.Application.Security;
using VepayDO.Contracts.Customers;

namespace VepayDO.Api.Controllers.Customers;

/// <summary>
/// Customer endpoints under <c>/api/v1/customers</c> (UC-DATA-01, UC-DATA-02).
/// Read and mutate allow OWNER and VERIFIER; moving an alias between customers is
/// OWNER-only (AGENTS.md section 9.5).
/// </summary>
[ApiController]
[Route("customers")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.CustomersManage)]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await customers.ListAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }

    [HttpGet("{customerId:guid}")]
    [RequirePermission(Permissions.CustomersManage)]
    public async Task<ActionResult<CustomerResponse>> Get(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(customerId, cancellationToken).ConfigureAwait(false);
        return Ok(customer.ToResponse());
    }

    [HttpPost]
    [RequirePermission(Permissions.CustomersManage)]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest body,
        CancellationToken cancellationToken)
    {
        var input = new CreateCustomerInput(body.Name, body.Phone, body.Notes);

        var customer = await customers
            .CreateAsync(input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, customer.ToResponse());
    }

    [HttpPatch("{customerId:guid}")]
    [RequirePermission(Permissions.CustomersManage)]
    public async Task<ActionResult<CustomerResponse>> Update(
        Guid customerId,
        [FromBody] UpdateCustomerRequest body,
        CancellationToken cancellationToken)
    {
        var input = new UpdateCustomerInput(body.Name, body.Phone, body.Notes, body.IsActive);

        var customer = await customers
            .UpdateAsync(customerId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(customer.ToResponse());
    }

    [HttpPost("{customerId:guid}/aliases")]
    [RequirePermission(Permissions.CustomersManage)]
    public async Task<ActionResult<CustomerResponse>> AddAlias(
        Guid customerId,
        [FromBody] AddAliasRequest body,
        CancellationToken cancellationToken)
    {
        var input = new AddAliasInput(body.Alias);

        var customer = await customers
            .AddAliasAsync(customerId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(customer.ToResponse());
    }

    [HttpPost("{customerId:guid}/aliases/move")]
    [RequirePermission(Permissions.CustomersAliasMove)]
    public async Task<ActionResult<CustomerResponse>> MoveAlias(
        Guid customerId,
        [FromBody] MoveAliasRequest body,
        CancellationToken cancellationToken)
    {
        var input = new MoveAliasInput(body.FromCustomerId, body.Alias);

        var customer = await customers
            .MoveAliasAsync(customerId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(customer.ToResponse());
    }
}
