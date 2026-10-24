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

    private async Task<(Guid TenantA, Guid UserA, Guid TenantB, Guid UserB)> SeedTwoTenantsAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var admin = await DbClient.ConnectAsync(_fixture.AppConnectionString);

        var userA = await admin.InsertUserAsync($"runner-a-{suffix}@example.test", "Runner A");
        var userB = await admin.InsertUserAsync($"runner-b-{suffix}@example.test", "Runner B");

        var tenantA = await admin.CreateTenantAsync($"Runner Tenant A {suffix}", userA);
        var tenantB = await admin.CreateTenantAsync($"Runner Tenant B {suffix}", userB);

        return (tenantA, userA, tenantB, userB);
    }

    private static Receipt NewReceipt(Guid tenantId, Guid uploadedBy, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        IdempotencyKey = Guid.NewGuid(),
        UploadedBy = uploadedBy,
        Source = ReceiptSource.WEB_UPLOAD,
        StorageKey = $"tenant/{tenantId}/receipts/{id}",
        Mime = "image/png",
        Bytes = 10,
        Sha256 = new byte[32],
        ProcessingStatus = ProcessingStatus.RECEIVED,
        ReconciliationStatus = ReconciliationStatus.PENDING_REVIEW,
        TransferType = TransferType.UNKNOWN,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
