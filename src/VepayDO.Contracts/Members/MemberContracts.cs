using VepayDO.Domain.Enums;

namespace VepayDO.Contracts.Members;

/// <summary>
/// Wire contracts for the members module (UC-SET-03, UC-AUTH-09). Depend only on
/// the Domain enums so the API and the client can share them (AGENTS.md section 6).
/// </summary>
public sealed record MemberResponse(
    Guid UserId,
    string Name,
    string Email,
    MemberRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Body of <c>POST /members</c>.</summary>
public sealed record CreateMemberRequest(string Email, string Name, MemberRole Role);

/// <summary>
/// Response of <c>POST /members</c>. <see cref="TemporaryPassword"/> is shown
/// exactly once and never returned again.
/// </summary>
public sealed record CreateMemberResponse(MemberResponse Member, string TemporaryPassword);

/// <summary>Body of <c>PATCH /members/{userId}</c>; both fields are optional.</summary>
public sealed record UpdateMemberRequest(MemberRole? Role, bool? IsActive);