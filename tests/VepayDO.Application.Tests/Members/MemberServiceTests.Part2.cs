using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Members;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Members;

public sealed partial class MemberServiceTests
{

    [Fact]
    public async Task Create_rejects_an_email_that_already_exists()
    {
        var harness = new Harness();
        harness.Users.Seed("taken@example.test");

        var act = async () => await harness.Service.CreateAsync(
            new CreateMemberInput("taken@example.test", "Taken", MemberRole.VERIFIER),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_writes_exactly_one_audit_event()
    {
        var harness = new Harness();

        await harness.Service.CreateAsync(
            new CreateMemberInput("v@example.test", "Verifier", MemberRole.VERIFIER),
            Meta(),
            CancellationToken.None);

        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Entity.Should().Be("membership");
        harness.Audit.Events[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task Update_deactivates_a_verifier_and_revokes_their_sessions()
    {
        var harness = new Harness();
        var verifierId = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");
        harness.Memberships.Seed(TenantId, verifierId, "Verifier", MemberRole.VERIFIER, "v@example.test");
        harness.Sessions.Seed(verifierId, Guid.NewGuid());

        var member = await harness.Service.UpdateAsync(
            verifierId,
            new UpdateMemberInput(Role: null, IsActive: false),
            Meta(),
            CancellationToken.None);

        member.IsActive.Should().BeFalse();
        harness.Memberships.ActiveOf(verifierId).Should().BeFalse();
        harness.Sessions.RevokedFor(verifierId).Should().BeTrue();
        harness.Revocation.Revoked.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Update_cannot_deactivate_the_last_active_owner()
    {
        var harness = new Harness();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");

        var act = async () => await harness.Service.UpdateAsync(
            OwnerId,
            new UpdateMemberInput(Role: null, IsActive: false),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
