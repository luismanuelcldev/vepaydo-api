using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Security;
using VepayDO.Contracts.BusinessAccounts;

namespace VepayDO.Api.Controllers.BusinessAccounts;

[ApiController]
[Route("business-accounts")]
[RequirePermission(Permissions.BusinessAccountsManage)]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "MVC actions must be instance members so model binding, filters and base members resolve.")]
public sealed class BusinessAccountsController(IBusinessAccountService accounts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessAccountResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }

    [HttpGet("{accountId:guid}")]
    public async Task<ActionResult<BusinessAccountResponse>> Get(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken).ConfigureAwait(false);
        return Ok(account.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<BusinessAccountResponse>> Create(
        [FromBody] CreateBusinessAccountRequest body,
        CancellationToken cancellationToken)
    {
        var input = new CreateBusinessAccountInput(body.BankId, body.Label, body.Last4, body.Currency);

        var account = await accounts
            .CreateAsync(input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, account.ToResponse());
    }

    [HttpPatch("{accountId:guid}")]
    public async Task<ActionResult<BusinessAccountResponse>> Update(
        Guid accountId,
        [FromBody] UpdateBusinessAccountRequest body,
        CancellationToken cancellationToken)
    {
        var input = new UpdateBusinessAccountInput(body.Label, body.IsActive);

        var account = await accounts
            .UpdateAsync(accountId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(account.ToResponse());
    }
}
