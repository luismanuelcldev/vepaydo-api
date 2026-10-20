using VepayDO.Domain.Enums;

namespace VepayDO.Application.Auth.Ports;

/// <summary>A membership row joined with the tenant (and, for member lists, the user).</summary>
public sealed record MembershipRecord
{
    public required Guid TenantId { get; init; }

    public required Guid UserId { get; init; }

    public required MemberRole Role { get; init; }

    public required bool IsActive { get; init; }

    public required string TenantName { get; init; }

    public string? UserName { get; init; }

    public string? UserEmail { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Reads and writes <c>memberships</c>. RLS exposes the current tenant plus the
/// caller's own rows (<c>memberships_own</c>), so membership reads run under the
/// real <c>app.user_id</c> (AGENTS.md section 7).
/// </summary>
public interface IMembershipRepository
{
    Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MembershipRecord?> FindAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
