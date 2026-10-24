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
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_returns_the_tenant_members()
    {
        var harness = new Harness();
        harness.Memberships.Seed(TenantId, OwnerId, "Owner", MemberRole.OWNER, "owner@example.test");

        var members = await harness.Service.ListAsync(CancellationToken.None);

        members.Should().ContainSingle();
        members[0].UserId.Should().Be(OwnerId);
        members[0].Role.Should().Be(MemberRole.OWNER);
    }

    [Fact]
    public async Task List_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Create_returns_a_temporary_password_once_and_forces_change()
    {
        var harness = new Harness();

        var result = await harness.Service.CreateAsync(
            new CreateMemberInput("  New.Verifier@Example.TEST ", "Nuevo Verificador", MemberRole.VERIFIER),
            Meta(),
            CancellationToken.None);

        result.TemporaryPassword.Should().NotBeNullOrEmpty();
        result.TemporaryPassword.Length.Should().BeGreaterThanOrEqualTo(12);
        result.Member.Email.Should().Be("new.verifier@example.test");
        result.Member.Role.Should().Be(MemberRole.VERIFIER);

        harness.Users.Created.Should().ContainSingle();
        var created = harness.Users.Created[0];
        created.MustChangePassword.Should().BeTrue();
        created.PasswordHash.Should().Be(harness.Hasher.Hash(result.TemporaryPassword));
    }

    [Fact]
    public async Task Create_uses_a_distinct_password_each_time()
    {
        var harness = new Harness();

        var first = await harness.Service.CreateAsync(
            new CreateMemberInput("a@example.test", "A", MemberRole.VERIFIER), Meta(), CancellationToken.None);
        var second = await harness.Service.CreateAsync(
            new CreateMemberInput("b@example.test", "B", MemberRole.VERIFIER), Meta(), CancellationToken.None);

        first.TemporaryPassword.Should().NotBe(second.TemporaryPassword);
    }
}
