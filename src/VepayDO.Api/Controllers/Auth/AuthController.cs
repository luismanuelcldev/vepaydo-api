using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Auth;
using VepayDO.Contracts.Auth;

namespace VepayDO.Api.Controllers.Auth;

[ApiController]
[Route("auth")]
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "MVC actions must be instance members.")]
public sealed partial class AuthController(IAuthService auth, IHostEnvironment environment) : ControllerBase
{
    private bool SecureCookies => environment.IsProduction();

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest body, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(new RegisterInput(body.Email, body.Name, body.Password, body.TenantName), RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);
        return StatusCode(StatusCodes.Status201Created, result.ToResponse());
    }

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest body, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(new LoginInput(body.Email, body.Password), RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);
        return Ok(result.ToResponse());
    }

    [CheckOrigin]
    [HttpPost("select-tenant")]
    public async Task<ActionResult<SelectTenantResponse>> SelectTenant([FromBody] SelectTenantRequest body, CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        var result = await auth.SelectTenantAsync(userId, body.TenantId, AuthCookies.ReadRefreshToken(HttpContext), RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }

    [Public]
    [CheckOrigin]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = AuthCookies.ReadRefreshToken(HttpContext) ?? string.Empty;
        var result = await auth.RefreshAsync(token, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);
        return Ok(new RefreshResponse(result.AccessToken));
    }

    [AllowPasswordChange]
    [AllowPendingLegal]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        await auth.LogoutAsync(userId, AuthCookies.ReadRefreshToken(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.ClearRefreshToken(HttpContext, SecureCookies);
        return NoContent();
    }

    [AllowPasswordChange]
    [AllowPendingLegal]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var result = await auth.MeAsync(User.RequireUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }
}
