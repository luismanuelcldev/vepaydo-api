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

    private sealed class FakeTokenService : ITokenService
    {
        private int _counter;

        public AccessTokenClaims? LastClaims { get; private set; }

        public string SignAccessToken(AccessTokenClaims claims)
        {
            ArgumentNullException.ThrowIfNull(claims);

            LastClaims = claims;
            return $"access-{claims.UserId:N}-{claims.TenantId?.ToString("N") ?? "none"}";
        }

        public Task<AccessTokenClaims> VerifyAccessTokenAsync(string token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IssuedRefreshToken IssueRefreshToken()
        {
            var token = $"refresh-{++_counter}-{Guid.NewGuid():N}";
            return new IssuedRefreshToken(token, HashRefreshToken(token));
        }

        public byte[] HashRefreshToken(string token) =>
            SHA256.HashData(Encoding.UTF8.GetBytes(token + Pepper));

        public bool HashesMatch(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
    }

    private sealed class FakeSessionRevocationStore : ISessionRevocationStore
    {
        public List<Guid> Revoked { get; } = [];

        public Task<bool> IsRevokedAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Revoked.Contains(sessionId));

        public Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            Revoked.Add(sessionId);
            return Task.CompletedTask;
        }
    }
}
