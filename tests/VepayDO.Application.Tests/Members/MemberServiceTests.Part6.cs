using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Members;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Members;

public sealed partial class MemberServiceTests
{

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, UserRecord> _byEmail = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, UserRecord> _byId = [];

        public List<NewUser> Created { get; } = [];

        public void Seed(string email)
        {
            var id = Guid.NewGuid();
            var record = new UserRecord
            {
                Id = id,
                Email = email,
                Name = email,
                PasswordHash = "hash:x",
                IsActive = true,
                PreferredLocale = "es-DO",
                MustChangePassword = false,
                IsPlatformSupport = false,
            };

            _byEmail[email] = record;
            _byId[id] = record;
        }

        public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byEmail.GetValueOrDefault(email));

        public Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.GetValueOrDefault(userId));

        public Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
        {
            Created.Add(user);
            var record = new UserRecord
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                PasswordHash = user.PasswordHash,
                IsActive = true,
                PreferredLocale = user.PreferredLocale,
                MustChangePassword = user.MustChangePassword,
                IsPlatformSupport = false,
            };

            _byEmail[user.Email] = record;
            _byId[user.Id] = record;
            return Task.CompletedTask;
        }

        public Task UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateLocaleAsync(Guid userId, string preferredLocale, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetPlatformSupportAsync(Guid userId, bool isPlatformSupport, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
