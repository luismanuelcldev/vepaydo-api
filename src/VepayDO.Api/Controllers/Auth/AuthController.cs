using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Auth;

namespace VepayDO.Api.Controllers.Auth;

/// <summary>
/// Authentication, account security, and session endpoints under
/// <c>/api/v1/auth</c> (AGENTS.md sections 9.9 and 10). Mirrors the reference
/// controller: the refresh token only travels in an httpOnly cookie, and the
/// cookie-authenticated routes (<c>select-tenant</c>, <c>refresh</c>) are guarded
/// by <c>[CheckOrigin]</c>.
/// </summary>
[ApiController]
[Route("auth")]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "MVC actions must be instance members so model binding, filters and base members resolve.")]
public sealed class AuthController(IAuthService auth, IHostEnvironment environment) : ControllerBase
{
    private bool SecureCookies => environment.IsProduction();

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest body,
        CancellationToken cancellationToken)
    {
        var result = await auth
            .RegisterAsync(new RegisterInput(body.Email, body.Name, body.Password, body.TenantName), Meta(), cancellationToken)
            .ConfigureAwait(false);

        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);

        return StatusCode(StatusCodes.Status201Created, AuthResponse.From(result));
    }

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest body,
        CancellationToken cancellationToken)
    {
        var result = await auth
            .LoginAsync(new LoginInput(body.Email, body.Password), Meta(), cancellationToken)
            .ConfigureAwait(false);

        AuthCookies.SetRefreshToken(HttpContext, result.RefreshToken, result.RefreshExpiresAt, SecureCookies);

        return Ok(AuthResponse.From(result));
    }

    [CheckOrigin]
    [HttpPost("select-tenant")]
    public async Task<ActionResult<SelectTenantResponse>> SelectTenant(
        [FromBody] SelectTenantRequest body,
        CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        var result = await auth
            .SelectTenantAsync(userId, body.TenantId, AuthCookies.ReadRefreshToken(HttpContext), Meta(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(SelectTenantResponse.From(result));
    }

    [Public]
    [CheckOrigin]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = AuthCookies.ReadRefreshToken(HttpContext) ?? string.Empty;
        var result = await auth.RefreshAsync(token, Meta(), cancellationToken).ConfigureAwait(false);

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
        return Ok(MeResponse.From(result));
    }

    [AllowPasswordChange]
    [AllowPendingLegal]
    [HttpPost("change-password")]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest body,
        CancellationToken cancellationToken)
    {
        var user = User.RequireUser();
        var accessToken = await auth
            .ChangePasswordAsync(user.UserId, body.CurrentPassword, body.NewPassword, user.SessionId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new ChangePasswordResponse(accessToken));
    }

    [AllowPasswordChange]
    [HttpPatch("me")]
    public async Task<ActionResult<UpdateLocaleResponse>> UpdateLocale(
        [FromBody] UpdateProfileRequest body,
        CancellationToken cancellationToken)
    {
        var locale = body.PreferredLocale ?? AuthConstants.DefaultLocale;
        await auth.UpdateLocaleAsync(User.RequireUserId(), locale, cancellationToken).ConfigureAwait(false);

        return Ok(new UpdateLocaleResponse(locale));
    }

    [Public]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest body,
        CancellationToken cancellationToken)
    {
        await auth.ForgotPasswordAsync(body.Email, Meta(), cancellationToken).ConfigureAwait(false);
        return Ok(new ForgotPasswordResponse(AuthConstants.ForgotPasswordNeutralMessage));
    }

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest body,
        CancellationToken cancellationToken)
    {
        await auth.ResetPasswordAsync(body.Token, body.NewPassword, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> ListSessions(CancellationToken cancellationToken)
    {
        var sessions = await auth
            .ListSessionsAsync(User.RequireUserId(), User.SessionIdOrNull(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(sessions.Select(SessionResponse.From).ToList());
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken)
    {
        await auth.RevokeSessionAsync(User.RequireUserId(), id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        await auth
            .RevokeAllOtherSessionsAsync(User.RequireUserId(), User.SessionIdOrNull(), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    private RequestMeta Meta()
    {
        var userAgent = Request.Headers.UserAgent.Count > 0 ? Request.Headers.UserAgent.ToString() : null;
        var deviceLabel = userAgent is null
            ? null
            : userAgent[..Math.Min(userAgent.Length, AuthConstants.DeviceLabelMaxLength)];

        return new RequestMeta(HttpContext.Connection.RemoteIpAddress, userAgent, deviceLabel);
    }
}
