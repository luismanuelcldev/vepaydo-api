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
    public async Task Audit_events_reject_updates_and_deletes_for_the_runtime_role()
    {
        var (tenantId, userId) = await SeedTenantAsync();

        await using var host = Host.Create(_fixture.AppConnectionString);
        var audit = new AuditEventRepository(host.Provider, Clock);

        var entityId = Guid.NewGuid();
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
                        EntityId = entityId,
                        Action = "reject",
                    },
                    ct);
                return true;
            });

        var updateAttempt = async () => await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await host.Provider.DbContext.Database.ExecuteSqlAsync(
                    $"UPDATE audit_events SET action = 'tampered' WHERE entity_id = {entityId}",
                    ct);
                return true;
            });

        await updateAttempt.Should().ThrowAsync<PostgresException>(
            "the immutability trigger rejects audit updates");

        var deleteAttempt = async () => await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            async ct =>
            {
                await host.Provider.DbContext.Database.ExecuteSqlAsync(
                    $"DELETE FROM audit_events WHERE entity_id = {entityId}",
                    ct);
                return true;
            });

        await deleteAttempt.Should().ThrowAsync<PostgresException>(
            "the immutability trigger rejects audit deletes");

        var stillThere = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantId, userId, DbRole.Owner),
            ct => new ValueTask<int>(
                host.Provider.DbContext.AuditEvents.AsNoTracking()
                    .CountAsync(a => a.EntityId == entityId && a.Action == "reject", ct)));

        stillThere.Should().Be(1);
    }
}
