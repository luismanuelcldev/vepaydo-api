using VepayDO.Domain.Enums;

namespace VepayDO.Contracts.Members;

public sealed record MemberResponse(
    Guid UserId,
    string Name,
    string Email,
    MemberRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateMemberRequest(string Email, string Name, MemberRole Role);

public sealed record CreateMemberResponse(MemberResponse Member, string TemporaryPassword);

public sealed record UpdateMemberRequest(MemberRole? Role, bool? IsActive);
