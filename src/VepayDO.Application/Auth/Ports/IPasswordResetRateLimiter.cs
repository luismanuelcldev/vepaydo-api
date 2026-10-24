namespace VepayDO.Application.Auth.Ports;

public interface IPasswordResetRateLimiter
{

    bool TryConsumeEmail(string email);

    bool TryConsumeIp(string ipAddress);

    bool IsWithinCooldown(string email);

    void MarkIssued(string email);
}
