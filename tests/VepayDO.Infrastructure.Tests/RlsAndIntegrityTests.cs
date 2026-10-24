using FluentAssertions;
using Npgsql;
using Xunit.Sdk;

namespace VepayDO.Infrastructure.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed partial class RlsAndIntegrityTests : IAsyncLifetime
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
}
