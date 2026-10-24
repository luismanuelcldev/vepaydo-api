using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Auth;

public sealed partial class AuthServiceTests
{

    private sealed class InMemoryLoginAttemptStore : ILoginAttemptStore
    {
        private const int MaxFailures = 5;
        private int _failures;

        public int Failures => _failures;

        public Task<bool> IsBlockedAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_failures >= MaxFailures);

        public Task RegisterFailureAsync(string key, CancellationToken cancellationToken = default)
        {
            _failures++;
            return Task.CompletedTask;
        }

        public Task ResetAsync(string key, CancellationToken cancellationToken = default)
        {
            _failures = 0;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPasswordResetRateLimiter : IPasswordResetRateLimiter
    {
        private readonly Dictionary<string, DateTimeOffset> _issued = new(StringComparer.Ordinal);

        public bool TryConsumeEmail(string email) => true;

        public bool TryConsumeIp(string ipAddress) => true;

        public bool IsWithinCooldown(string email) =>
            _issued.TryGetValue(email, out var issuedAt) && issuedAt + AuthConstants.ForgotPasswordResendCooldown > Now;

        public void MarkIssued(string email) => _issued[email] = Now;
    }

    private sealed class NullTenantContextAccessor : ITenantContextAccessor
    {
        public TenantContext? Current => null;
    }
}
