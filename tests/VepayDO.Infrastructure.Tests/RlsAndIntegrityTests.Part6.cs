using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class RlsAndIntegrityTests
{

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
