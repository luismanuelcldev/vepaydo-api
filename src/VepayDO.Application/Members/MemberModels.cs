using VepayDO.Domain.Enums;

namespace VepayDO.Application.Members;

/// <summary>A tenant member as returned by <c>GET /members</c> (UC-SET-03).</summary>
public sealed record MemberView(
    Guid UserId,
    string Name,
    string Email,
    MemberRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// Result of creating a member (UC-SET-03). <see cref="TemporaryPassword"/> is
/// returned exactly once, at creation time, and never persisted or logged.
/// </summary>
public sealed record CreatedMemberResult(MemberView Member, string TemporaryPassword);

/// <summary>New member payload: the user account plus its initial role.</summary>
public sealed record CreateMemberInput(string Email, string Name, MemberRole Role);

/// <summary>Role/active change payload for <c>PATCH /members/{userId}</c>.</summary>
public sealed record UpdateMemberInput(MemberRole? Role, bool? IsActive);