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

    /// <summary>
    /// Native clients (X-Client-Type: native) receive refresh tokens in the
    /// response body; browser clients only through the httpOnly cookie (ADR 0006).
    /// </summary>
    private AuthResponse WithNativeRefreshToken(AuthResponse response, string refreshToken) =>
        ClientTypeHeaders.IsNative(HttpContext.Request)
            ? response with { RefreshToken = refreshToken }
            : response;

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest body, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(new RegisterInput(body.Email, body.Name, body.Password, body.TenantName), RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);

        var response = result.ToResponse();
        return StatusCode(StatusCodes.Status201Created, WithNativeRefreshToken(response, result.RefreshToken));
    }

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest body, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(new LoginInput(body.Email, body.Password), RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);

        var response = result.ToResponse();
        return Ok(WithNativeRefreshToken(response, result.RefreshToken));
    }

    [CheckOrigin]
    [HttpPost("select-tenant")]
    public async Task<ActionResult<SelectTenantResponse>> SelectTenant([FromBody] SelectTenantRequest body, CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        var refreshToken = !string.IsNullOrEmpty(body.RefreshToken)
            ? body.RefreshToken
            : AuthCookies.ReadRefreshToken(HttpContext);
        var result = await auth.SelectTenantAsync(userId, body.TenantId, refreshToken, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return Ok(result.ToResponse());
    }

    [Public]
    [CheckOrigin]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh([FromBody] RefreshRequest? body, CancellationToken cancellationToken)
    {
        var isNative = ClientTypeHeaders.IsNative(HttpContext.Request);
        string? nativeToken = body?.RefreshToken;
        var token = isNative && !string.IsNullOrEmpty(nativeToken)
            ? nativeToken
            : AuthCookies.ReadRefreshToken(HttpContext) ?? string.Empty;

        var result = await auth.RefreshAsync(token, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);
        return Ok(new RefreshResponse(result.AccessToken)
        {
            RefreshToken = isNative ? result.RefreshToken : null,
        });
    }

    [AllowPasswordChange]
    [AllowPendingLegal]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? body, CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        string? nativeToken = body?.RefreshToken;
        var token = ClientTypeHeaders.IsNative(HttpContext.Request) && !string.IsNullOrEmpty(nativeToken)
            ? nativeToken
            : AuthCookies.ReadRefreshToken(HttpContext);

        await auth.LogoutAsync(userId, token, cancellationToken).ConfigureAwait(false);
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
