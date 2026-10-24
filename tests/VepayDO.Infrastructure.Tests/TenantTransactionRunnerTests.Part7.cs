using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class TenantTransactionRunnerTests
{

    [Fact]
    public async Task The_audit_log_is_appended_by_any_role_but_read_only_by_the_owner()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);

        var inserted = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Verifier),
            async ct => await host.Provider.DbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO audit_events (tenant_id, user_id, actor, entity, action, created_at)
                VALUES ({tenantA}, {userA}, 'USER', 'receipt', 'confirm', now())
                """,
                ct));

        inserted.Should().Be(1);

        var verifierReads = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Verifier),
            async ct => await host.Provider.DbContext.AuditEvents.AsNoTracking()
                .CountAsync(a => a.TenantId == tenantA && a.Action == "confirm", ct));

        verifierReads.Should().Be(0, "the audit SELECT policy is owner-only");

        var ownerReads = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct => await host.Provider.DbContext.AuditEvents.AsNoTracking()
                .CountAsync(a => a.TenantId == tenantA && a.Action == "confirm", ct));

        ownerReads.Should().Be(1);
    }
}
