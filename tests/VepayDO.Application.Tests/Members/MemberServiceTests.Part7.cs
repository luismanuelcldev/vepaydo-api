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

    private sealed class FakeMembershipRepository : IMembershipRepository
    {
        private readonly Dictionary<Guid, MembershipRecord> _byUser = [];

        public void Seed(Guid tenantId, Guid userId, string name, MemberRole role, string email)
        {
            _byUser[userId] = new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = "Tenant",
                UserName = name,
                UserEmail = email,
                CreatedAt = Now,
            };
        }

        public bool ActiveOf(Guid userId) => _byUser[userId].IsActive;

        public Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _byUser.Values.Where(m => m.UserId == userId && m.IsActive).ToList();
            return Task.FromResult(items);
        }

        public Task<MembershipRecord?> FindAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byUser.GetValueOrDefault(userId));

        public Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _byUser.Values.Where(m => m.TenantId == tenantId).ToList();
            return Task.FromResult(items);
        }

        public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byUser.Values.Count(m => m.TenantId == tenantId && m.IsActive && m.Role == MemberRole.OWNER));

        public Task AddAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
        {
            _byUser[userId] = new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = "Tenant",
                UserName = userId.ToString(),
                UserEmail = userId.ToString(),
                CreatedAt = Now,
            };

            return Task.CompletedTask;
        }

        public Task UpdateRoleAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
        {
            _byUser[userId] = _byUser[userId] with { Role = role };
            return Task.CompletedTask;
        }

        public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken = default)
        {
            _byUser[userId] = _byUser[userId] with { IsActive = isActive };
            return Task.CompletedTask;
        }
    }
}
