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

    private sealed class FakeMembershipRepository : IMembershipRepository
    {
        private readonly List<MembershipRecord> _memberships = [];

        public void Seed(Guid tenantId, Guid userId, string tenantName, MemberRole role)
        {
            _memberships.Add(new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = tenantName,
                CreatedAt = Now,
            });
        }

        public Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _memberships
                .Where(m => m.UserId == userId && m.IsActive)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<MembershipRecord?> FindAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            var match = _memberships.FirstOrDefault(m => m.TenantId == tenantId && m.UserId == userId);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MembershipRecord> items = _memberships.Where(m => m.TenantId == tenantId).ToList();
            return Task.FromResult(items);
        }

        public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_memberships.Count(m => m.TenantId == tenantId && m.IsActive && m.Role == MemberRole.OWNER));

        public Task AddAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
        {
            _memberships.Add(new MembershipRecord
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                IsActive = true,
                TenantName = "Added",
                CreatedAt = Now,
            });

            return Task.CompletedTask;
        }

        public Task UpdateRoleAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
