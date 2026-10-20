using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Application.Security;

namespace VepayDO.Api.Tests.Probe;

/// <summary>
/// Test-only endpoints. They exist so the kernel can be exercised end to end:
/// every route declares exactly the metadata the pipeline is supposed to honour,
/// and the route prefix convention turns them into <c>/api/v1/__test/...</c>.
/// </summary>
[ApiController]
[Route("__test")]
public sealed class ProbeController(RecordingTenantTransactionRunner runner) : ControllerBase
{
    [HttpGet("anonymous")]
    [Public]
    public IActionResult Anonymous() => Ok(new { ok = true });

    [HttpGet("secure")]
    public IActionResult Secure() => Ok(new { ok = true });

    [HttpGet("context")]
    public IActionResult Context()
    {
        var context = runner.LastContext;
        return Ok(new
        {
            tenantId = context?.TenantId,
            userId = context?.UserId,
            role = context?.Role?.ToString(),
            runCount = runner.RunContexts.Count,
        });
    }

    [HttpGet("owner-only")]
    [RequirePermission(Permissions.ReceiptsRevert)]
    public IActionResult OwnerOnly() => Ok(new { ok = true });

    [HttpGet("verifier-allowed")]
    [RequirePermission(Permissions.ReceiptsReview)]
    public IActionResult VerifierAllowed() => Ok(new { ok = true });

    [HttpGet("password-change")]
    [AllowPasswordChange]
    public IActionResult PasswordChange() => Ok(new { ok = true });

    [HttpGet("legal-allowed")]
    [AllowPendingLegal]
    public IActionResult LegalAllowed() => Ok(new { ok = true });

    [HttpGet("origin")]
    [Public]
    [CheckOrigin]
    public IActionResult OriginChecked() => Ok(new { ok = true });

    [HttpGet("boom")]
    [Public]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "MVC only discovers instance action methods, so the probe must stay an instance method.")]
    public IActionResult Boom() => throw new InvalidOperationException("secret internal detail");

    [HttpPost("validation")]
    [Public]
    public IActionResult Validation([FromBody] SampleRequest request) =>
        Ok(new { name = request.Name });
}
