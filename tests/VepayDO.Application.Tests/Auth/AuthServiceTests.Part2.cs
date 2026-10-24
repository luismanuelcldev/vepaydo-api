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
    public async Task Login_locks_out_after_repeated_failures()
    {
        var harness = new Harness();
        harness.Users.Seed(Guid.NewGuid(), "user@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.LoginAsync(
            new LoginInput("user@example.test", "wrong-password1"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        await act.Should().ThrowAsync<TooManyRequestsException>();
    }

    [Fact]
    public async Task Login_selects_the_only_membership()
    {
        var harness = new Harness();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        harness.Users.Seed(userId, "owner@example.test", password: "correct-horse12");
        harness.Memberships.Seed(tenantId, userId, "Mi tienda", MemberRole.OWNER);

        var result = await harness.Service.LoginAsync(
            new LoginInput("owner@example.test", "correct-horse12"),
            new RequestMeta(null, "ua", "ua"),
            CancellationToken.None);

        result.Memberships.Should().ContainSingle();
        harness.Tokens.LastClaims!.TenantId.Should().Be(tenantId);
        harness.Users.LastLoginAt!.Value.Should().Be(Now);
    }

    [Fact]
    public async Task Login_without_any_membership_is_forbidden()
    {
        var harness = new Harness();
        harness.Users.Seed(Guid.NewGuid(), "orphan@example.test", password: "correct-horse12");

        var act = async () => await harness.Service.LoginAsync(
            new LoginInput("orphan@example.test", "correct-horse12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
