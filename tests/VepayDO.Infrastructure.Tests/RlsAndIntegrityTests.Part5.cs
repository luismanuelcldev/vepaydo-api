using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class RlsAndIntegrityTests
{
    [Fact]
    public async Task Support_reads_no_tenant_rows_directly_and_needs_an_active_grant()
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();
        Guid receiptId;
        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync("INSERT INTO business_accounts (tenant_id, bank_id, label, last4, currency, updated_at) VALUES (app.current_tenant_id(), (SELECT id FROM banks WHERE code = 'popular'), 'Main', '1234', 'DOP', now())");
            receiptId = await owner.InsertReceiptAsync(amount: 100m);
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            (await support.CountAsync("receipts")).Should().Be(0);
            (await support.CountAsync("business_accounts")).Should().Be(0);
            var tenantsVisible = (long)(await support.ScalarAsync("SELECT count(*) FROM app.support_tenant_overview()"))!;
            tenantsVisible.Should().BeGreaterThanOrEqualTo(1);
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var denied = await CaptureAsync(() => support.ScalarAsync("SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId));
            denied.SqlState.Should().Be(InsufficientPrivilege);
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var writeDenied = await CaptureAsync(() => support.ExecuteAsync("INSERT INTO business_accounts (tenant_id, bank_id, label, last4, currency, updated_at) VALUES ($1, (SELECT id FROM banks WHERE code = 'popular'), 'X', '0000', 'DOP', now())", tenantA.TenantId));
            writeDenied.SqlState.Should().Be(InsufficientPrivilege);
        }

        Guid grantId;
        await using (var owner = await Connect(tenantA))
        {
            grantId = (Guid)(await owner.ScalarAsync("INSERT INTO support_grants (tenant_id, granted_by, expires_at) VALUES (app.current_tenant_id(), app.current_user_id(), now() + interval '1 hour') RETURNING id"))!;
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var visible = (long)(await support.ScalarAsync("SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId))!;
            visible.Should().BeGreaterThanOrEqualTo(1);
        }

        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync("UPDATE support_grants SET revoked_at = now() WHERE id = $1", grantId);
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var revoked = await CaptureAsync(() => support.ScalarAsync("SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId));
            revoked.SqlState.Should().Be(InsufficientPrivilege);
        }

        await using (var owner = await Connect(tenantA))
        {
            await owner.ExecuteAsync("INSERT INTO support_grants (tenant_id, granted_by, expires_at, created_at) VALUES (app.current_tenant_id(), app.current_user_id(), now() - interval '1 minute', now() - interval '1 hour')");
            await owner.CommitAsync();
        }

        await using (var support = await DbClient.ConnectAsync(_fixture.AppConnectionString))
        {
            await support.UseContextAsync(null, null, "support");
            var expired = await CaptureAsync(() => support.ScalarAsync("SELECT count(*) FROM app.support_list_receipts($1)", tenantA.TenantId));
            expired.SqlState.Should().Be(InsufficientPrivilege);
        }
        _ = receiptId;
    }
}
