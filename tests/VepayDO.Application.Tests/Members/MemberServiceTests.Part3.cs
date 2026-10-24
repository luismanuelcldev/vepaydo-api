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
    public async Task Update_cannot_demote_the_last_active_owner()
    {
        var harness = new Harness();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");

        var act = async () => await harness.Service.UpdateAsync(
            OwnerId,
            new UpdateMemberInput(Role: MemberRole.VERIFIER, IsActive: true),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Update_can_demote_an_owner_when_another_active_owner_exists()
    {
        var harness = new Harness();
        var secondOwner = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");
        harness.Memberships.Seed(TenantId, secondOwner, "Owner 2", MemberRole.OWNER, "owner2@example.test");

        var member = await harness.Service.UpdateAsync(
            OwnerId,
            new UpdateMemberInput(Role: MemberRole.VERIFIER, IsActive: true),
            Meta(),
            CancellationToken.None);

        member.Role.Should().Be(MemberRole.VERIFIER);
    }

    [Fact]
    public async Task Update_promotes_a_verifier_to_owner()
    {
        var harness = new Harness();
        var verifierId = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");
        harness.Memberships.Seed(TenantId, verifierId, "Verifier", MemberRole.VERIFIER, "v@example.test");

        var member = await harness.Service.UpdateAsync(
            verifierId,
            new UpdateMemberInput(Role: MemberRole.OWNER, IsActive: true),
            Meta(),
            CancellationToken.None);

        member.Role.Should().Be(MemberRole.OWNER);
        harness.Audit.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_of_an_unknown_member_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMemberInput(Role: MemberRole.VERIFIER, IsActive: true),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
