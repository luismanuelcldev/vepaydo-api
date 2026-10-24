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
    public async Task Refresh_with_an_expired_session_is_rejected()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var issued = harness.Tokens.IssueRefreshToken();
        harness.Sessions.Seed(new SessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExpiresAt = Now.AddMinutes(-1),
            CreatedAt = Now.AddDays(-31),
        }, issued.Hash);

        var act = async () => await harness.Service.RefreshAsync(
            issued.Token,
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Change_password_rejects_a_wrong_current_password()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.ChangePasswordAsync(
            userId,
            "not-the-password",
            "brand-new-secret12",
            sessionId: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Change_password_updates_the_hash_and_clears_the_flag()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "temporary123", mustChangePassword: true);
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var token = await harness.Service.ChangePasswordAsync(
            userId,
            "temporary123",
            "brand-new-secret12",
            sessionId: null,
            CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        harness.Users.MustChangePassword.Should().BeFalse();
        harness.Users.Verify(userId, "brand-new-secret12").Should().BeTrue();
        harness.Audit.Events.Should().Contain(e => e.Action == "change_password");
    }
}
