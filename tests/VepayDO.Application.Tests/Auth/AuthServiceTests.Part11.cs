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

    private sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly List<(PasswordResetTokenRecord Record, byte[] Hash)> _tokens = [];

        public IReadOnlyList<PasswordResetTokenRecord> All => _tokens.Select(entry => entry.Record).ToList();

        public Task CreateAsync(NewPasswordResetToken token, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(token);

            _tokens.Add((
                new PasswordResetTokenRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = token.UserId,
                    ExpiresAt = token.ExpiresAt,
                    UsedAt = null,
                },
                token.TokenHash));

            return Task.CompletedTask;
        }

        public Task<PasswordResetTokenRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default)
        {

            var match = _tokens.LastOrDefault(entry => entry.Record.UsedAt is null);
            if (match.Record is null && _tokens.Count > 0)
            {
                match = _tokens[^1];
            }

            return Task.FromResult<PasswordResetTokenRecord?>(match.Record);
        }

        public Task InvalidateUnusedForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < _tokens.Count; index++)
            {
                var entry = _tokens[index];
                if (entry.Record.UserId == userId && entry.Record.UsedAt is null)
                {
                    _tokens[index] = (entry.Record with { UsedAt = at }, entry.Hash);
                }
            }

            return Task.CompletedTask;
        }

        public Task MarkUsedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var index = _tokens.FindIndex(entry => entry.Record.Id == id);
            if (index >= 0)
            {
                _tokens[index] = (_tokens[index].Record with { UsedAt = at }, _tokens[index].Hash);
            }

            return Task.CompletedTask;
        }
    }
}
