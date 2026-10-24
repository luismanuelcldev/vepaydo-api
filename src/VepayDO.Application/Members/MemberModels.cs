using VepayDO.Domain.Enums;

namespace VepayDO.Application.Members;

public sealed record MemberView(
    Guid UserId,
    string Name,
    string Email,
    MemberRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreatedMemberResult(MemberView Member, string TemporaryPassword);

public sealed record CreateMemberInput(string Email, string Name, MemberRole Role);

public sealed record UpdateMemberInput(MemberRole? Role, bool? IsActive);
