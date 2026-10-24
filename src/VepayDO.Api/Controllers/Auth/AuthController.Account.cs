using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Auth;
using VepayDO.Contracts.Auth;

namespace VepayDO.Api.Controllers.Auth;

public sealed partial class AuthController
{
    [AllowPasswordChange]
    [AllowPendingLegal]
    [HttpPost("change-password")]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken cancellationToken)
    {
        var user = User.RequireUser();
        var accessToken = await auth.ChangePasswordAsync(user.UserId, body.CurrentPassword, body.NewPassword, user.SessionId, cancellationToken).ConfigureAwait(false);
        return Ok(new ChangePasswordResponse(accessToken));
    }

    [AllowPasswordChange]
    [HttpPatch("me")]
    public async Task<ActionResult<UpdateLocaleResponse>> UpdateLocale([FromBody] UpdateProfileRequest body, CancellationToken cancellationToken)
    {
        var locale = body.PreferredLocale ?? AuthConstants.DefaultLocale;
        await auth.UpdateLocaleAsync(User.RequireUserId(), locale, cancellationToken).ConfigureAwait(false);
        return Ok(new UpdateLocaleResponse(locale));
    }

    [Public]
    [EnableRateLimiting("auth-strict")]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest body, CancellationToken cancellationToken)
    {
        await auth.ForgotPasswordAsync(body.Email, RequestMetaFactory.From(HttpContext), cancellationToken).ConfigureAwait(false);
        return Ok(new ForgotPasswordResponse(AuthConstants.ForgotPasswordNeutralMessage));
    }

    [Public]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body, CancellationToken cancellationToken)
    {
        await auth.ResetPasswordAsync(body.Token, body.NewPassword, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> ListSessions(CancellationToken cancellationToken)
    {
        var sessions = await auth.ListSessionsAsync(User.RequireUserId(), User.SessionIdOrNull(), cancellationToken).ConfigureAwait(false);
        return Ok(sessions.Select(s => s.ToResponse()).ToList());
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
        await auth.RevokeAllOtherSessionsAsync(User.RequireUserId(), User.SessionIdOrNull(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
