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
    public async Task Refresh_rotates_the_session_and_revokes_the_old_one()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var login = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        var oldSession = harness.Sessions.All.Single();

        var refreshed = await harness.Service.RefreshAsync(
            login.RefreshToken,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        refreshed.AccessToken.Should().NotBeNullOrEmpty();
        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);

        harness.Sessions.All.Should().HaveCount(2);
        harness.Sessions.All.Single(s => s.Id == oldSession.Id).RevokedAt.Should().NotBeNull();
        harness.Revocation.Revoked.Should().Contain(oldSession.Id);
    }

    [Fact]
    public async Task Refresh_with_a_revoked_token_burns_the_whole_family()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var login = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        _ = await harness.Service.RefreshAsync(
            login.RefreshToken,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        var lastLoginAfterFirstLogin = harness.Users.LastLoginAt;

        var replay = async () => await harness.Service.RefreshAsync(
            login.RefreshToken,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await replay.Should().ThrowAsync<UnauthorizedException>();
        harness.Users.LastLoginAt.Should().Be(lastLoginAfterFirstLogin, "reuse never issues a new session");
        harness.Audit.Events.Should().Contain(e => e.Reason == "refresh_token_reuse");
    }
}
