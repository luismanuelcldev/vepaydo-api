using System.Security.Cryptography;
using System.Text;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Auth;

public sealed partial class AuthService : IAuthService
{
    private const string DefaultTenantName = "Mi negocio";
    private const string InvalidCredentials = "Invalid credentials";
    private const string InvalidSession = "Invalid session";

    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IMembershipRepository _memberships;
    private readonly ITenantFactory _tenantFactory;
    private readonly IAuditWriter _audit;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly ILoginAttemptStore _lockout;
    private readonly IPasswordResetRateLimiter _resetLimiter;
    private readonly ISessionRevocationStore _revocation;
    private readonly IMailSender _mail;
    private readonly AppConfig _config;
    private readonly IClock _clock;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ITenantTransactionRunner _runner;

    public AuthService(
        IUserRepository users, ISessionRepository sessions, IPasswordResetTokenRepository resetTokens,
        IMembershipRepository memberships, ITenantFactory tenantFactory, IAuditWriter audit,
        ITokenService tokens, IPasswordHasher hasher, ILoginAttemptStore lockout,
        IPasswordResetRateLimiter resetLimiter, ISessionRevocationStore revocation, IMailSender mail,
        AppConfig config, IClock clock, ITenantContextAccessor tenantContext, ITenantTransactionRunner runner)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _resetTokens = resetTokens ?? throw new ArgumentNullException(nameof(resetTokens));
        _memberships = memberships ?? throw new ArgumentNullException(nameof(memberships));
        _tenantFactory = tenantFactory ?? throw new ArgumentNullException(nameof(tenantFactory));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _lockout = lockout ?? throw new ArgumentNullException(nameof(lockout));
        _resetLimiter = resetLimiter ?? throw new ArgumentNullException(nameof(resetLimiter));
        _revocation = revocation ?? throw new ArgumentNullException(nameof(revocation));
        _mail = mail ?? throw new ArgumentNullException(nameof(mail));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    private async Task<CreatedSession> CreateSessionAsync(Guid userId, Guid? tenantId, RequestMeta meta, CancellationToken cancellationToken)
    {
        var issued = _tokens.IssueRefreshToken();
        var expiresAt = _clock.UtcNow + TokenConstants.RefreshTokenLifetime;
        var sessionId = await _sessions.CreateAsync(new NewSession(userId, tenantId, issued.Hash, expiresAt, meta.UserAgent, meta.DeviceLabel, meta.Ip, _clock.UtcNow), cancellationToken);
        return new CreatedSession(sessionId, issued.Token, expiresAt);
    }

    private static List<MembershipSummary> ToSummaries(IReadOnlyList<MembershipRecord> memberships, bool mustChangePassword) =>
        memberships.Select(m => new MembershipSummary(m.TenantId, m.TenantName, m.Role, mustChangePassword)).ToList();

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string NewOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private Task<TResult> InUserTxAsync<TResult>(Guid userId, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken) =>
        _tenantContext.Current is null ? _runner.RunAsUserAsync(userId, work, cancellationToken) : work(cancellationToken).AsTask();

    private Task<TResult> InNoneTxAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken) =>
        _tenantContext.Current is null ? _runner.RunAsync(TenantContext.None, work, cancellationToken) : work(cancellationToken).AsTask();

    private Task<TResult> InContextTxAsync<TResult>(TenantContext context, Func<CancellationToken, ValueTask<TResult>> work, CancellationToken cancellationToken) =>
        _tenantContext.Current is null ? _runner.RunAsync(context, work, cancellationToken) : work(cancellationToken).AsTask();

    private sealed record CreatedSession(Guid SessionId, string RefreshToken, DateTimeOffset RefreshExpiresAt);
}
