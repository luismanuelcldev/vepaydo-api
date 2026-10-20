namespace VepayDO.Application.Auth.Ports;

/// <summary>
/// Rate limits for password-reset requests (UC-AUTH-04): a per-email window, a
/// per-IP window, and a resend cooldown per email. In-process for now; replaced
/// by Redis in M7 (AGENTS.md section 9.9).
/// </summary>
public interface IPasswordResetRateLimiter
{
    /// <summary>Consumes one email slot; returns <c>false</c> when the limit is exceeded.</summary>
    bool TryConsumeEmail(string email);

    /// <summary>Consumes one IP slot; returns <c>false</c> when the limit is exceeded.</summary>
    bool TryConsumeIp(string ipAddress);

    /// <summary>Whether a link was emailed for this account within the cooldown window.</summary>
    bool IsWithinCooldown(string email);

    /// <summary>Records that a reset link was just emailed for this account.</summary>
    void MarkIssued(string email);
}
