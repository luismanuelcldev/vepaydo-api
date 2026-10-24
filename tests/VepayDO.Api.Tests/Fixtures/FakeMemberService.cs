using VepayDO.Application.Auth;
using VepayDO.Application.Members;
using VepayDO.Domain.Enums;

namespace VepayDO.Api.Tests.Fixtures;

public sealed class FakeMemberService : IMemberService
{
    public IReadOnlyList<MemberView> Members { get; set; } = [];

    public string TemporaryPassword { get; set; } = "temp-password-1234";

    public Exception? Error { get; set; }

    public CreateMemberInput? LastCreate { get; private set; }

    public void Reset()
    {
        Members = [];
        TemporaryPassword = "temp-password-1234";
        Error = null;
        LastCreate = null;
    }

    public Task<IReadOnlyList<MemberView>> ListAsync(CancellationToken cancellationToken = default) =>
        Error is not null
            ? Task.FromException<IReadOnlyList<MemberView>>(Error)
            : Task.FromResult(Members);

    public Task<CreatedMemberResult> CreateAsync(
        CreateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        LastCreate = input;

        if (Error is not null)
        {
            return Task.FromException<CreatedMemberResult>(Error);
        }

        var member = new MemberView(
            Guid.NewGuid(),
            input.Name,
            input.Email,
            input.Role,
            IsActive: true,
            DateTimeOffset.UtcNow);

        return Task.FromResult(new CreatedMemberResult(member, TemporaryPassword));
    }

    public Task<MemberView> UpdateAsync(
        Guid userId,
        UpdateMemberInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Error is not null)
        {
            return Task.FromException<MemberView>(Error);
        }

        return Task.FromResult(new MemberView(
            userId,
            "Member",
            "member@example.test",
            input.Role ?? MemberRole.VERIFIER,
            input.IsActive ?? true,
            DateTimeOffset.UtcNow));
    }

    public Task RevokeSessionsAsync(Guid userId, RequestMeta meta, CancellationToken cancellationToken = default) =>
        Error is not null
            ? Task.FromException(Error)
            : Task.CompletedTask;
}
