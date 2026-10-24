using FluentAssertions;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Tenancy;

public sealed partial class TenantContextTests
{
    [Theory]
    [InlineData(DbRole.Owner, "owner")]
    [InlineData(DbRole.Verifier, "verifier")]
    [InlineData(DbRole.System, "system")]
    [InlineData(DbRole.Support, "support")]
    public void Roles_are_written_as_lowercase_values(DbRole role, string expected)
    {
        role.ToDatabaseValue().Should().Be(expected);
        DbRoleExtensions.FromDatabaseValue(expected).Should().Be(role);
    }

    [Fact]
    public void An_unknown_role_cannot_be_written()
    {
        var unknown = (DbRole)99;

        var act = () => unknown.ToDatabaseValue();

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Theory]
    [InlineData("OWNER")]
    [InlineData("Owner")]
    [InlineData("")]
    [InlineData("root")]
    public void An_unknown_database_value_is_rejected(string value)
    {
        var act = () => DbRoleExtensions.FromDatabaseValue(value);

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void FromDatabaseValue_rejects_null()
    {
        var act = () => DbRoleExtensions.FromDatabaseValue(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Member_roles_map_to_the_database_roles()
    {
        VepayDO.Domain.Enums.MemberRole.OWNER.ToDbRole().Should().Be(DbRole.Owner);
        VepayDO.Domain.Enums.MemberRole.VERIFIER.ToDbRole().Should().Be(DbRole.Verifier);
    }

    [Fact]
    public void None_writes_every_setting_as_an_empty_string()
    {

        TenantContext.None.TenantIdSetting.Should().BeEmpty();
        TenantContext.None.UserIdSetting.Should().BeEmpty();
        TenantContext.None.RoleSetting.Should().BeEmpty();
    }

    [Fact]
    public void ForUser_leaves_the_tenant_and_the_role_unset()
    {
        var userId = Guid.NewGuid();
        var context = TenantContext.ForUser(userId);

        context.TenantId.Should().BeNull();
        context.UserId.Should().Be(userId);
        context.Role.Should().BeNull();

        context.TenantIdSetting.Should().BeEmpty();
        context.UserIdSetting.Should().Be(userId.ToString("D"));
        context.RoleSetting.Should().BeEmpty();
    }
}
