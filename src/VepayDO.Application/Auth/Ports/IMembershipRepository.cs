using VepayDO.Domain.Enums;

namespace VepayDO.Application.Auth.Ports;

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
