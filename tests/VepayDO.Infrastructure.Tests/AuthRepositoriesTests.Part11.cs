using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class AuthRepositoriesTests
{

    [Fact]
    public async Task MembershipRepository_hides_members_of_another_tenant()
    {
        var (tenantA, ownerA) = await SeedTenantAsync();
        var (tenantB, ownerB) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var memberships = new MembershipRepository(host.Provider, Clock);

        var crossRead = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantB, ownerB, DbRole.Owner),
            ct => new ValueTask<IReadOnlyList<MembershipRecord>>(
                memberships.ListByTenantAsync(tenantA, ct)));

        crossRead.Should().BeEmpty("RLS keeps tenant A rows out of tenant B's scope");
    }

    [Fact]
    public async Task AuditEventRepository_writes_append_only_rows_visible_to_the_owner()
    {
        var (tenantId, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var audit = new AuditEventRepository(host.Provider, Clock);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await audit.WriteAsync(
                    new AuditEventDraft
                    {
                        TenantId = tenantId,
                        UserId = userId,
                        Actor = AuditActor.USER,
                        Entity = "receipt",
                        EntityId = Guid.NewGuid(),
                        Action = "confirm",
                        Before = """{"status":"PENDING_REVIEW"}""",
                        After = """{"status":"CONFIRMED"}""",
                        Reason = null,
                        Ip = IPAddress.Parse("190.80.1.2"),
                    },
                    ct);
                return true;
            });

        var rows = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            ct => new ValueTask<int>(
                host.Provider.DbContext.AuditEvents.AsNoTracking()
                    .CountAsync(a => a.TenantId == tenantId && a.Action == "confirm", ct)));

        rows.Should().Be(1);

        var hiddenFromVerifier = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Verifier),
            ct => new ValueTask<int>(
                host.Provider.DbContext.AuditEvents.AsNoTracking()
                    .CountAsync(a => a.TenantId == tenantId && a.Action == "confirm", ct)));

        hiddenFromVerifier.Should().Be(0, "the audit SELECT policy is owner-only");
    }
}
