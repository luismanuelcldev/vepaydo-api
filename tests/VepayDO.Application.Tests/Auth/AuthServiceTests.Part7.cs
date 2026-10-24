using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Auth;

public sealed partial class AuthServiceTests
{

    [Fact]
    public async Task Select_tenant_activates_the_context_and_audits()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var result = await harness.Service.SelectTenantAsync(
            userId,
            tenantId,
            refreshToken: null,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        harness.Runner.Activated.Should().ContainSingle();
        harness.Runner.Activated[0].TenantId.Should().Be(tenantId);
        harness.Audit.Events.Should().Contain(e => e.Action == "select_tenant");
        harness.Tokens.LastClaims!.Role.Should().Be(nameof(MemberRole.OWNER));
    }

    [Fact]
    public async Task Select_tenant_is_forbidden_without_a_membership()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.SelectTenantAsync(
            userId,
            Guid.NewGuid(),
            refreshToken: null,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Revoke_all_other_sessions_keeps_the_current_one()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var first = harness.NewSession(userId);
        var current = harness.NewSession(userId);

        await harness.Service.RevokeAllOtherSessionsAsync(userId, current, CancellationToken.None);

        harness.Sessions.All.Single(s => s.Id == first).RevokedAt.Should().NotBeNull();
        harness.Sessions.All.Single(s => s.Id == current).RevokedAt.Should().BeNull();
    }
}
