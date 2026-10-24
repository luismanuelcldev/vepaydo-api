using VepayDO.Domain.Enums;

namespace VepayDO.Application.Security;

public sealed record AuthenticatedUser
{
    public required Guid UserId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? TenantId { get; init; }

    public MemberRole? Role { get; init; }

    public bool MustChangePassword { get; init; }
}
