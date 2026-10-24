using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class RlsAndIntegrityTests
{

    [Fact]
    public async Task Allocations_cannot_mix_currencies()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using var owner = await Connect(tenantA);
        var receiptId = await owner.InsertReceiptAsync(amount: 100m, currency: "DOP");
        var chargeId = await owner.InsertChargeAsync("ALLOC-FX", 100m, currency: "USD");

        var error = await CaptureAsync(() => owner.ExecuteAsync(
            "INSERT INTO allocations (tenant_id, receipt_id, charge_id, amount_applied, created_by) " +
            "VALUES (app.current_tenant_id(), $1, $2, $3, app.current_user_id())",
            receiptId, chargeId, 50m));

        error.SqlState.Should().Be(CheckViolation);
    }

    [Fact]
    public async Task Only_create_tenant_can_create_tenants()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using var owner = await Connect(tenantA);
        var error = await CaptureAsync(() => owner.ExecuteAsync(
            "INSERT INTO tenants (name, updated_at) VALUES ('rogue', now())"));

        error.SqlState.Should().Be(InsufficientPrivilege);
    }

    [Fact]
    public async Task A_verifier_cannot_revert_a_final_status()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using (var owner = await Connect(tenantA))
        {
            var receiptId = await owner.InsertReceiptAsync(amount: 100m);
            await ConfirmAsOwnerAsync(owner, receiptId);
            await owner.CommitAsync();
        }

        await using var verifier = await Connect(tenantA, tenantA.VerifierId);
        var error = await CaptureAsync(() => verifier.ExecuteAsync(
            "UPDATE receipts SET reconciliation_status = 'PENDING_REVIEW' WHERE reconciliation_status = 'CONFIRMED'"));

        error.SqlState.Should().Be(InsufficientPrivilege);
        error.MessageText.Should().Contain("owner can revert");
    }

    [Fact]
    public async Task A_verifier_cannot_confirm_above_the_amount_cap()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using var verifier = await Connect(tenantA, tenantA.VerifierId);

        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync(
                "UPDATE tenant_settings SET verifier_amount_cap = 50 WHERE tenant_id = app.current_tenant_id()");
            await owner.InsertReceiptAsync(amount: 100m);
            await owner.CommitAsync();
        }

        var error = await CaptureAsync(() => verifier.ExecuteAsync(
            "UPDATE receipts SET reconciliation_status = 'CONFIRMED', payment_date = current_date, " +
            "bank_id = (SELECT id FROM banks WHERE code = 'popular'), decided_by = app.current_user_id(), decided_at = now() " +
            "WHERE reconciliation_status = 'PENDING_REVIEW'"));

        error.SqlState.Should().Be(InsufficientPrivilege);
        error.MessageText.Should().Contain("cap");
    }
}
