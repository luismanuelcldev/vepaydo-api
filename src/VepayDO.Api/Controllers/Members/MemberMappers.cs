using VepayDO.Application.Members;
using VepayDO.Contracts.Members;

namespace VepayDO.Api.Controllers.Members;

/// <summary>
/// Maps the member application results onto the wire contracts, keeping
/// <c>VepayDO.Contracts</c> free of any Application dependency (AGENTS.md section 6).
/// </summary>
internal static class MemberMappers
{
    public static MemberResponse ToResponse(this MemberView member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberResponse(
            member.UserId,
            member.Name,
            member.Email,
            member.Role,
            member.IsActive,
            member.CreatedAt);
    }

    public static IReadOnlyList<MemberResponse> ToResponse(this IReadOnlyList<MemberView> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        return members.Select(ToResponse).ToList();
    }

    public static CreateMemberResponse ToResponse(this CreatedMemberResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new CreateMemberResponse(result.Member.ToResponse(), result.TemporaryPassword);
    }
}