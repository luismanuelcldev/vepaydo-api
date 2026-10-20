using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Required RLS and integrity coverage (AGENTS.md section 7): tenant isolation,
/// composite foreign keys, fail-closed behavior with no context, verifier rules,
/// append-only audit, allocation limits, tenant creation, and support access.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RlsAndIntegrityTests : IAsyncLifetime
{
    private const string InsufficientPrivilege = "42501";
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    private readonly PostgresFixture _fixture;

    public RlsAndIntegrityTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Without_context_the_app_connection_sees_zero_rows()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using (var owner = await Connect(tenantA))
        {
            await owner.InsertChargeAsync("CTX-1", 100m);
            await owner.CommitAsync();
        }

        await using var anonymous = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        (await anonymous.CountAsync("charges")).Should().Be(0);
        (await anonymous.CountAsync("customers")).Should().Be(0);
        (await anonymous.CountAsync("receipts")).Should().Be(0);
        (await anonymous.CountAsync("business_accounts")).Should().Be(0);
        (await anonymous.CountAsync("allocations")).Should().Be(0);
        (await anonymous.CountAsync("alerts")).Should().Be(0);
    }

    [Fact]
    public async Task A_tenant_reads_only_its_own_rows()
    {
        var (tenantA, tenantB) = await SeedTwoTenantsAsync();

        await using (var ownerA = await Connect(tenantA))
        {
            await ownerA.InsertChargeAsync("ISO-1", 100m);
            await ownerA.CommitAsync();
        }

        await using (var ownerA = await Connect(tenantA))
        {
            (await ownerA.CountAsync("charges")).Should().Be(1);
        }

        await using (var ownerB = await Connect(tenantB))
        {
            (await ownerB.CountAsync("charges")).Should().Be(0);
        }
    }

    [Fact]
    public async Task A_tenant_cannot_write_a_row_for_another_tenant()
    {
        var (tenantA, tenantB) = await SeedTwoTenantsAsync();

        await using var ownerA = await Connect(tenantA);

        var error = await CaptureAsync(() => ownerA.ExecuteAsync(
            "INSERT INTO charges (tenant_id, code, description, amount, currency, updated_at) " +
            "VALUES ($1, 'X', 'X', 1, 'DOP', now())",
            tenantB.TenantId));

        error.SqlState.Should().Be(InsufficientPrivilege);
        error.MessageText.Should().Contain("row-level security");
    }

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

        // Each rejected statement opens its own transaction: the first error aborts
        // the transaction, so a second probe on the same transaction would fail with
        // 25P02 (in_failed_sql_transaction) instead of the privilege error.
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

        // Owner sets a 50 cap, then a 100 receipt is waiting.
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

    [Fact]
    public async Task Support_reads_no_tenant_rows_directly_and_needs_an_active_grant()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        Guid receiptId;
        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync(
                "INSERT INTO business_accounts (tenant_id, bank_id, label, last4, currency, updated_at) " +
                "VALUES (app.current_tenant_id(), (SELECT id FROM banks WHERE code = 'popular'), 'Main', '1234', 'DOP', now())");
            receiptId = await owner.InsertReceiptAsync(amount: 100m);
            await owner.CommitAsync();
        }

        // Support session: role = support, no tenant context.
        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");

            (await support.CountAsync("receipts")).Should().Be(0);
            (await support.CountAsync("business_accounts")).Should().Be(0);

            var tenantsVisible = (long)(await support.ScalarAsync(
                "SELECT count(*) FROM app.support_tenant_overview()"))!;
            tenantsVisible.Should().BeGreaterThanOrEqualTo(1);
        }

        // Without a grant the business-data function refuses (own transaction: the
        // failure would otherwise abort it for the next probe).
        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var denied = await CaptureAsync(() => support.ScalarAsync(
                "SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId));
            denied.SqlState.Should().Be(InsufficientPrivilege);
        }

        // Support can never write tenant data (no tenant context => RLS blocks).
        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var writeDenied = await CaptureAsync(() => support.ExecuteAsync(
                "INSERT INTO business_accounts (tenant_id, bank_id, label, last4, currency, updated_at) " +
                "VALUES ($1, (SELECT id FROM banks WHERE code = 'popular'), 'X', '0000', 'DOP', now())",
                tenantA.TenantId));
            writeDenied.SqlState.Should().Be(InsufficientPrivilege);
        }

        // Owner grants read-only access for one hour.
        Guid grantId;
        await using (var owner = await Connect(tenantA))
        {
            grantId = (Guid)(await owner.ScalarAsync(
                "INSERT INTO support_grants (tenant_id, granted_by, expires_at) " +
                "VALUES (app.current_tenant_id(), app.current_user_id(), now() + interval '1 hour') RETURNING id"))!;
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var visible = (long)(await support.ScalarAsync(
                "SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId))!;
            visible.Should().BeGreaterThanOrEqualTo(1);
        }

        // Revoking the grant removes access immediately.
        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync(
                "UPDATE support_grants SET revoked_at = now() WHERE id = $1", grantId);
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var revoked = await CaptureAsync(() => support.ScalarAsync(
                "SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId));
            revoked.SqlState.Should().Be(InsufficientPrivilege);
        }

        // An expired grant also fails.
        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync(
                "INSERT INTO support_grants (tenant_id, granted_by, expires_at, created_at) " +
                "VALUES (app.current_tenant_id(), app.current_user_id(), now() - interval '1 minute', now() - interval '1 hour')");
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var expired = await CaptureAsync(() => support.ScalarAsync(
                "SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId));
            expired.SqlState.Should().Be(InsufficientPrivilege);
        }

        _ = receiptId;
    }

    private static async Task ConfirmAsOwnerAsync(DbClient owner, Guid receiptId)
    {
        await owner.ExecuteAsync(
            "UPDATE receipts SET reconciliation_status = 'CONFIRMED', amount = 100, currency = 'DOP', " +
            "payment_date = current_date, bank_id = (SELECT id FROM banks WHERE code = 'popular'), " +
            "decided_by = app.current_user_id(), decided_at = now() WHERE id = $1",
            receiptId);
    }

    private async Task<DbClient> Connect(SeededTenant tenant, Guid? asUser = null)
    {
        var client = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        await client.UseContextAsync(tenant.TenantId, asUser ?? tenant.OwnerId, asUser is null ? "owner" : "verifier");
        return client;
    }

    private async Task<(SeededTenant A, SeededTenant B)> SeedTwoTenantsAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);
        var ownerA = await admin.InsertUserAsync($"owner-a-{suffix}@example.test", "Owner A");
        var verifierA = await admin.InsertUserAsync($"verifier-a-{suffix}@example.test", "Verifier A");
        var ownerB = await admin.InsertUserAsync($"owner-b-{suffix}@example.test", "Owner B");
        var tenantA = await admin.CreateTenantAsync($"Tenant A {suffix}", ownerA);
        var tenantB = await admin.CreateTenantAsync($"Tenant B {suffix}", ownerB);

        await using (var members = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await members.UseContextAsync(tenantA, ownerA, "owner");
            await members.ExecuteAsync(
                "INSERT INTO memberships (tenant_id, user_id, role) VALUES (app.current_tenant_id(), $1, 'VERIFIER')",
                verifierA);
            await members.CommitAsync();
        }

        return (new SeededTenant(tenantA, ownerA, verifierA), new SeededTenant(tenantB, ownerB, ownerB));
    }

    private static async Task<PostgresException> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception)
        {
            return exception;
        }

        throw new XunitException("Expected a PostgresException, but the statement succeeded.");
    }

    private sealed record SeededTenant(Guid TenantId, Guid OwnerId, Guid VerifierId);
}
