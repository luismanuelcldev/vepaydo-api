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
    public async Task Reset_password_consumes_the_token_and_revokes_every_session()
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

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        var sessionId = harness.Sessions.All.Single(s => s.RevokedAt is null).Id;

        await harness.Service.ResetPasswordAsync(
            "the-emailed-token",
            "another-new-secret12",
            CancellationToken.None);

        harness.Users.Verify(userId, "another-new-secret12").Should().BeTrue();
        harness.ResetTokens.All.Single().UsedAt.Should().NotBeNull();
        harness.Revocation.Revoked.Should().Contain(sessionId);
        harness.Audit.Events.Should().Contain(e => e.Action == "reset_password");
        harness.Mail.Sent.Should().Contain(m => m.Subject.Contains("contraseña cambiada", StringComparison.Ordinal));
        login.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reset_password_rejects_a_reused_token()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await harness.Service.ResetPasswordAsync("the-emailed-token", "another-new-secret12", CancellationToken.None);

        var act = async () => await harness.Service.ResetPasswordAsync(
            "the-emailed-token",
            "yet-another-secret12",
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
