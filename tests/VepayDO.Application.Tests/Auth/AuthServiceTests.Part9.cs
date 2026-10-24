using VepayDO.Application.Auth.Ports;

namespace VepayDO.Application.Tests.Auth;

public sealed partial class AuthServiceTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, NewUser> _users = [];
        private readonly Dictionary<string, Guid> _byEmail = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, Guid> ByEmail => _byEmail;
        public DateTimeOffset? LastLoginAt { get; private set; }
        public bool MustChangePassword { get; private set; }
        private string _passwordHash = string.Empty;

        public void Seed(Guid userId, string email, string password = "supersecret12", bool mustChangePassword = false)
        {
            _passwordHash = FakePasswordHasher.Compute(password);
            _users[userId] = new NewUser(userId, email, "Seeded", _passwordHash, "es-DO", mustChangePassword);
            _byEmail[email] = userId;
        }

        public bool Verify(Guid userId, string password) =>
            _users.TryGetValue(userId, out _) && _passwordHash == FakePasswordHasher.Compute(password);

        public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (!_byEmail.TryGetValue(email, out var id) || !_users.TryGetValue(id, out var user))
                return Task.FromResult<UserRecord?>(null);
            MustChangePassword = user.MustChangePassword;
            return Task.FromResult<UserRecord?>(ToRecord(user));
        }

        public Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user)) return Task.FromResult<UserRecord?>(null);
            MustChangePassword = user.MustChangePassword;
            return Task.FromResult<UserRecord?>(ToRecord(user));
        }

        public Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            _users[user.Id] = user;
            _byEmail[user.Email] = user.Id;
            _passwordHash = user.PasswordHash;
            MustChangePassword = user.MustChangePassword;
            return Task.CompletedTask;
        }

        public Task UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
        {
            if (_users.TryGetValue(userId, out var user)) _users[userId] = user with { PasswordHash = passwordHash };
            _passwordHash = passwordHash;
            MustChangePassword = mustChangePassword;
            return Task.CompletedTask;
        }

        public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) { LastLoginAt = at; return Task.CompletedTask; }
        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetPlatformSupportAsync(Guid userId, bool isPlatformSupport, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private static UserRecord ToRecord(NewUser user) => new()
        {
            Id = user.Id, Email = user.Email, Name = user.Name, PasswordHash = user.PasswordHash,
            IsActive = true, PreferredLocale = user.PreferredLocale, MustChangePassword = user.MustChangePassword, IsPlatformSupport = false,
        };
    }
}
