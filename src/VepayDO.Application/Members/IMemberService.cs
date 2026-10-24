using VepayDO.Application.Auth;

namespace VepayDO.Application.Members;

public interface IMemberService
{

    Task<IReadOnlyList<MemberView>> ListAsync(CancellationToken cancellationToken = default);

    Task<CreatedMemberResult> CreateAsync(
        CreateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task<MemberView> UpdateAsync(
        Guid userId,
        UpdateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    Task RevokeSessionsAsync(Guid userId, RequestMeta meta, CancellationToken cancellationToken = default);
}
