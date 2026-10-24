using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class RlsAndIntegrityTests
{

    [Fact]
    public async Task A_verifier_cannot_confirm_with_an_open_critical_alert()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using (var owner = await Connect(tenantA))
        {
            var receiptId = await owner.InsertReceiptAsync(amount: 100m);
            await owner.ExecuteAsync(
                "INSERT INTO alerts (tenant_id, receipt_id, rule, severity, message) " +
                "VALUES (app.current_tenant_id(), $1, 'DUPLICATE_REFERENCE', 'CRITICAL', 'dup')",
                receiptId);
            await owner.CommitAsync();
        }

        await using var verifier = await Connect(tenantA, tenantA.VerifierId);
        var error = await CaptureAsync(() => verifier.ExecuteAsync(
            "UPDATE receipts SET reconciliation_status = 'CONFIRMED', payment_date = current_date, " +
            "bank_id = (SELECT id FROM banks WHERE code = 'popular'), decided_by = app.current_user_id(), decided_at = now() " +
            "WHERE reconciliation_status = 'PENDING_REVIEW'"));

        error.SqlState.Should().Be(InsufficientPrivilege);
        error.MessageText.Should().Contain("critical");
    }
}
