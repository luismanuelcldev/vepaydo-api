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
    public async Task A_critical_alert_is_untouchable_by_a_verifier_but_not_by_the_system_role()
    {
        var (tenantA, userA, _, _) = await SeedTwoTenantsAsync();
        var receiptId = Guid.NewGuid();
        var alertId = Guid.NewGuid();

        await using var host = Host.Create(_fixture.AppConnectionString);

        await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct =>
            {
                var db = host.Provider.DbContext;
                db.Receipts.Add(NewReceipt(tenantA, userA, receiptId));
                db.Alerts.Add(new Alert
                {
                    Id = alertId,
                    TenantId = tenantA,
                    ReceiptId = receiptId,
                    Rule = AlertRuleCode.DUPLICATE_REFERENCE,
                    Severity = AlertSeverity.CRITICAL,
                    Message = "This reference was already used",
                    Details = "{}",
                    Status = AlertStatus.OPEN,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                return true;
            });

        var verifierRows = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Verifier),
            async ct => await host.Provider.DbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE alerts SET status = 'RESOLVED', resolution = 'REJECTED',
                                  resolved_by = {userA}, resolved_at = now(),
                                  resolution_reason = 'ignored by the test'
                WHERE id = {alertId}
                """,
                ct));

        verifierRows.Should().Be(0, "the alerts_update policy hides CRITICAL rows from a verifier");

        var systemRows = await host.Runner.RunAsSystemAsync(
            tenantA,
            async ct => await host.Provider.DbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE alerts SET status = 'RESOLVED', resolution = 'AUTO_CLEARED',
                                  resolved_at = now()
                WHERE id = {alertId}
                """,
                ct));

        systemRows.Should().Be(1, "the system role may re-evaluate alerts");

        var status = await host.Runner.RunAsync(
            TenantContext.ForTenant(tenantA, userA, DbRole.Owner),
            async ct => await host.Provider.DbContext.Alerts.AsNoTracking()
                .Where(a => a.Id == alertId)
                .Select(a => a.Status)
                .SingleAsync(ct));

        status.Should().Be(AlertStatus.RESOLVED);
    }
}
