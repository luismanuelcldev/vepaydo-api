using FluentAssertions;
using VepayDO.Infrastructure.Migrations;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class SchemaMigrationsTests
{

    [Fact]
    public async Task Row_level_security_is_enabled_and_forced_on_tenant_tables()
    {
        await using var owner = await DbClient.ConnectAsync(_fixture.OwnerConnectionString);

        foreach (var table in ExpectedRlsTables)
        {
            var enabled = await owner.ScalarAsync(
                "SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid = $1::regclass",
                "public." + table);

            enabled.Should().Be(true, $"RLS should be enabled and forced on {table}");
        }
    }

    [Fact]
    public async Task Context_functions_exist()
    {
        await using var owner = await DbClient.ConnectAsync(_fixture.OwnerConnectionString);

        var functions = new[]
        {
            "app.current_tenant_id()",
            "app.current_user_id()",
            "app.current_member_role()",
            "app.create_tenant(text, uuid)",
            "app.has_active_support_grant(uuid)",
            "app.hamming_distance(bigint, bigint)",
        };

        foreach (var function in functions)
        {
            var exists = await owner.ScalarAsync("SELECT to_regprocedure($1) IS NOT NULL", function);
            exists.Should().Be(true, $"function {function} should exist");
        }
    }
}
