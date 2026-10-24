using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Members;
using VepayDO.Application.Security;
using VepayDO.Contracts.Members;

namespace VepayDO.Api.Controllers.Members;

[ApiController]
[Route("members")]
[RequirePermission(Permissions.MembersManage)]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "MVC actions must be instance members so model binding, filters and base members resolve.")]
public sealed class MembersController(IMemberService members) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await members.ListAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<CreateMemberResponse>> Create(
        [FromBody] CreateMemberRequest body,
        CancellationToken cancellationToken)
    {
        var input = new CreateMemberInput(body.Email, body.Name, body.Role);

        var result = await members
            .CreateAsync(input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created, result.ToResponse());
    }

    [HttpPatch("{userId:guid}")]
    public async Task<ActionResult<MemberResponse>> Update(
        Guid userId,
        [FromBody] UpdateMemberRequest body,
        CancellationToken cancellationToken)
    {
        var input = new UpdateMemberInput(body.Role, body.IsActive);

        var member = await members
            .UpdateAsync(userId, input, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(member.ToResponse());
    }

    [HttpPost("{userId:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(Guid userId, CancellationToken cancellationToken)
    {
        await members
            .RevokeSessionsAsync(userId, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }
}
