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
    public async Task RevokeSessions_revokes_every_session_of_the_member()
    {
        var harness = new Harness();
        var verifierId = Guid.NewGuid();
        harness.Memberships.Seed(TenantId, verifierId, "Verifier", MemberRole.VERIFIER, "v@example.test");
        harness.Sessions.Seed(verifierId, Guid.NewGuid());
        harness.Sessions.Seed(verifierId, Guid.NewGuid());

        await harness.Service.RevokeSessionsAsync(verifierId, Meta(), CancellationToken.None);

        harness.Sessions.RevokedFor(verifierId).Should().BeTrue();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("revoke_member_sessions");
    }

    [Fact]
    public async Task RevokeSessions_of_an_unknown_member_is_not_found()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.RevokeSessionsAsync(Guid.NewGuid(), Meta(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static RequestMeta Meta() => new(null, null, null);

    private sealed class Harness
    {
        public Harness(bool useNullContext = false)
        {
            Users = new FakeUserRepository();
            Memberships = new FakeMembershipRepository();
            Sessions = new FakeSessionRepository();
            Revocation = new FakeSessionRevocationStore();
            Hasher = new FakePasswordHasher();
            Audit = new RecordingAuditWriter();
            var ctx = useNullContext ? null : TenantContext.ForTenant(TenantId, OwnerId, DbRole.Owner);
            Service = new MemberService(
                Users,
                Memberships,
                Sessions,
                Revocation,
                Hasher,
                Audit,
                new FixedClock(Now),
                new FakeTenantContextAccessor(ctx));
        }

        public FakeUserRepository Users { get; }

        public FakeMembershipRepository Memberships { get; }

        public FakeSessionRepository Sessions { get; }

        public FakeSessionRevocationStore Revocation { get; }

        public FakePasswordHasher Hasher { get; }

        public RecordingAuditWriter Audit { get; }

        public MemberService Service { get; }
    }
}
