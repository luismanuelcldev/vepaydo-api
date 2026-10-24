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
    private const string Pepper = "pepper";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Register_creates_the_user_tenant_and_session()
    {
        var harness = new Harness();

        var result = await harness.Service.RegisterAsync(
            new RegisterInput("  Alice@Example.TEST ", "Alice", "supersecret12", "Mi tienda"),
            new RequestMeta(null, "pytest", "pytest"),
            CancellationToken.None);

        result.UserId.Should().NotBeEmpty();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.Memberships.Should().ContainSingle();
        result.Memberships[0].Role.Should().Be(MemberRole.OWNER);
        result.Memberships[0].TenantName.Should().Be("Mi tienda");

        harness.Users.ByEmail.Should().ContainKey("alice@example.test");
        harness.TenantFactory.CreateCalls.Should().Be(1);
        harness.Sessions.All.Should().ContainSingle();
        harness.Tokens.LastClaims!.TenantId.Should().Be(result.Memberships[0].TenantId);
        harness.Tokens.LastClaims.Role.Should().Be(nameof(MemberRole.OWNER));
    }

    [Fact]
    public async Task Register_rejects_an_email_that_already_exists()
    {
        var harness = new Harness();
        var existing = Guid.NewGuid();
        harness.Users.Seed(existing, "taken@example.test");

        var act = async () => await harness.Service.RegisterAsync(
            new RegisterInput("taken@example.test", "Taken", "supersecret12", "Mi tienda"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_with_an_unknown_email_throws_and_records_a_failure()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.LoginAsync(
            new LoginInput("nobody@example.test", "supersecret12"),
            new RequestMeta(null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        harness.Lockout.Failures.Should().Be(1);
    }
}
