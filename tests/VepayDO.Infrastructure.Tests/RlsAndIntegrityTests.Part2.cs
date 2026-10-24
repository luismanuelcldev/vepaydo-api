using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class RlsAndIntegrityTests
{

    [Fact]
    public async Task Composite_foreign_keys_reject_cross_tenant_references()
    {
        var (tenantA, tenantB) = await SeedTwoTenantsAsync();

        Guid customerInB;
        await using (var ownerB = await Connect(tenantB))
        {
            customerInB = (Guid)(await ownerB.ScalarAsync(
                "INSERT INTO customers (tenant_id, name, name_normalized, updated_at) " +
                "VALUES (app.current_tenant_id(), 'C', 'c', now()) RETURNING id"))!;
            await ownerB.CommitAsync();
        }

        await using var ownerA = await Connect(tenantA);
        var error = await CaptureAsync(() => ownerA.ExecuteAsync(
            "INSERT INTO customer_aliases (tenant_id, customer_id, alias_normalized) " +
            "VALUES (app.current_tenant_id(), $1, 'alias')",
            customerInB));

        error.SqlState.Should().Be(ForeignKeyViolation);
    }

    [Fact]
    public async Task Audit_events_reject_update_and_delete_for_the_app_role()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync(
                "INSERT INTO audit_events (tenant_id, entity, entity_id, action) VALUES (app.current_tenant_id(), 'x', NULL, 'create')");
            await owner.CommitAsync();
        }

        await using (var owner = await Connect(tenantA))
        {
            var updateError = await CaptureAsync(() =>
                owner.ExecuteAsync("UPDATE audit_events SET action = 'tampered'"));
            updateError.SqlState.Should().Be(InsufficientPrivilege);
        }

        await using (var owner = await Connect(tenantA))
        {
            var deleteError = await CaptureAsync(() =>
                owner.ExecuteAsync("DELETE FROM audit_events"));
            deleteError.SqlState.Should().Be(InsufficientPrivilege);
        }
    }

    [Fact]
    public async Task Allocations_cannot_exceed_the_receipt_amount()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using var owner = await Connect(tenantA);
        var receiptId = await owner.InsertReceiptAsync(amount: 100m);
        var chargeId = await owner.InsertChargeAsync("ALLOC-BIG", 200m);

        var error = await CaptureAsync(() => owner.ExecuteAsync(
            "INSERT INTO allocations (tenant_id, receipt_id, charge_id, amount_applied, created_by) " +
            "VALUES (app.current_tenant_id(), $1, $2, $3, app.current_user_id())",
            receiptId, chargeId, 150m));

        error.SqlState.Should().Be(CheckViolation);
    }
}
