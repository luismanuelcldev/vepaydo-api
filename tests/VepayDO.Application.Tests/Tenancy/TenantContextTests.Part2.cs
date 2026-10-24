using FluentAssertions;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Tenancy;

public sealed partial class TenantContextTests
{

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
