using FluentAssertions;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Tenancy;

/// <summary>
/// The tenant context is only ever delivered to PostgreSQL as three strings, so the
/// mapping between the CLR side and the <c>app.tenant_id</c> / <c>app.user_id</c> /
/// <c>app.role</c> settings is the whole contract. A typo here would make every RLS
/// policy fail closed (or, worse, open) in production.
/// </summary>
public sealed class TenantContextTests
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
        // set_config with '' makes app.current_* return NULL, which is what fails closed.
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

    [Fact]
    public void ForTenant_sets_the_three_settings()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var context = TenantContext.ForTenant(tenantId, userId, DbRole.Verifier);

        context.TenantIdSetting.Should().Be(tenantId.ToString("D"));
        context.UserIdSetting.Should().Be(userId.ToString("D"));
        context.RoleSetting.Should().Be("verifier");
    }

    [Fact]
    public void ForSystem_acts_on_a_tenant_without_a_user()
    {
        var tenantId = Guid.NewGuid();
        var context = TenantContext.ForSystem(tenantId);

        context.TenantId.Should().Be(tenantId);
        context.UserId.Should().BeNull();
        context.Role.Should().Be(DbRole.System);

        context.UserIdSetting.Should().BeEmpty();
        context.RoleSetting.Should().Be("system");
    }

    [Fact]
    public void Guid_settings_use_the_lowercase_hyphenated_form_postgresql_returns()
    {
        var tenantId = Guid.Parse("0b6f8f9e-1c2d-4e3f-8a9b-0c1d2e3f4a5b");

        // app.current_tenant_id()::text yields the lowercase "D" format, so the value
        // written by set_config must match it for comparisons inside policies.
        TenantContext.ForTenant(tenantId, Guid.NewGuid(), DbRole.Owner).TenantIdSetting
            .Should().Be("0b6f8f9e-1c2d-4e3f-8a9b-0c1d2e3f4a5b");
    }

    [Fact]
    public void Contexts_with_different_components_are_not_equal()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var owner = TenantContext.ForTenant(tenantId, userId, DbRole.Owner);
        var verifier = TenantContext.ForTenant(tenantId, userId, DbRole.Verifier);

        owner.Should().NotBe(verifier);
        owner.Should().Be(TenantContext.ForTenant(tenantId, userId, DbRole.Owner));
    }
}
