using VepayDO.Domain.Enums;

namespace VepayDO.Application.Security;

/// <summary>
/// Identity attached to the request once the access token has been verified.
/// <see cref="TenantId"/> and <see cref="Role"/> are present only when the token
/// was issued for a selected tenant; a token without them can only reach
/// pre-tenant routes.
/// </summary>
public sealed record AuthenticatedUser
{
    public required Guid UserId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? TenantId { get; init; }

    public MemberRole? Role { get; init; }

    public bool MustChangePassword { get; init; }
}
