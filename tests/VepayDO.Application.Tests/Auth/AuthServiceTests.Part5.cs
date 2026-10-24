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
    public async Task Forgot_password_emails_a_link_once_within_the_cooldown()
    {
        var harness = new Harness();
        harness.Users.Seed(Guid.NewGuid(), "owner@example.test", password: "correct-horse12");

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await harness.Service.ForgotPasswordAsync(
            "owner@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        harness.Mail.Sent.Should().ContainSingle();
        harness.ResetTokens.All.Should().ContainSingle();
        harness.Mail.Sent[0].TextBody.Should().Contain("/reset-password?token=");
    }

    [Fact]
    public async Task Forgot_password_is_silent_for_an_unknown_account()
    {
        var harness = new Harness();

        await harness.Service.ForgotPasswordAsync(
            "missing@example.test",
            new RequestMeta(null, null, null),
            CancellationToken.None);

        harness.Mail.Sent.Should().BeEmpty();
        harness.ResetTokens.All.Should().BeEmpty();
    }
}
