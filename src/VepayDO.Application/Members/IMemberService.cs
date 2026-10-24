using VepayDO.Application.Auth;

namespace VepayDO.Application.Members;

/// <summary>
/// Member management use cases (UC-SET-03). Every method runs inside the
/// authenticated request transaction; the tenant comes from the ambient context
/// and the role gate (OWNER) is enforced by the route policy.
/// </summary>
public interface IMemberService
{
    /// <summary>Lists every member of the current tenant, active or not.</summary>
    Task<IReadOnlyList<MemberView>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a member: the user account (if the email is new) with a one-time
    /// temporary password, plus the membership. The password is returned once.
    /// </summary>
    Task<CreatedMemberResult> CreateAsync(
        CreateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a member's role and/or active flag. Deactivating a member revokes
    /// all of their sessions; the last active owner can neither be demoted nor
    /// deactivated.
    /// </summary>
    Task<MemberView> UpdateAsync(
        Guid userId,
        UpdateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session of the given member (UC-AUTH-09).</summary>
    Task RevokeSessionsAsync(Guid userId, RequestMeta meta, CancellationToken cancellationToken = default);
}