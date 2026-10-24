using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    private static readonly Type[] TenantScopedEntityTypes =
    [
        typeof(TenantSettings),
        typeof(Membership),
        typeof(PushDevice),
        typeof(BusinessAccount),
        typeof(Customer),
        typeof(CustomerAlias),
        typeof(Charge),
        typeof(Receipt),
        typeof(Extraction),
        typeof(Allocation),
        typeof(Alert),
        typeof(UxEvent),
        typeof(SupportGrant),
    ];

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        foreach (var type in TenantScopedEntityTypes)
        {
            Reference(modelBuilder, type, typeof(Tenant), [TenantIdColumn], null, DeleteBehavior.Cascade);
        }

        Reference(modelBuilder, typeof(AuditEvent), typeof(Tenant), [TenantIdColumn], null, DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(CustomerAlias), typeof(Customer),
            [TenantIdColumn, "CustomerId"], [TenantIdColumn, nameof(Customer.Id)], DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(Charge), typeof(Customer),
            [TenantIdColumn, "CustomerId"], [TenantIdColumn, nameof(Customer.Id)], DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Receipt), typeof(BusinessAccount),
            [TenantIdColumn, "DestinationAccountId"], [TenantIdColumn, nameof(BusinessAccount.Id)], DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Receipt), typeof(Customer),
            [TenantIdColumn, "CustomerId"], [TenantIdColumn, nameof(Customer.Id)], DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Receipt), typeof(Charge),
            [TenantIdColumn, "SuggestedChargeId"], [TenantIdColumn, nameof(Charge.Id)], DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Extraction), typeof(Receipt),
            [TenantIdColumn, "ReceiptId"], [TenantIdColumn, nameof(Receipt.Id)], DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(Allocation), typeof(Receipt),
            [TenantIdColumn, "ReceiptId"], [TenantIdColumn, nameof(Receipt.Id)], DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Allocation), typeof(Charge),
            [TenantIdColumn, "ChargeId"], [TenantIdColumn, nameof(Charge.Id)], DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Alert), typeof(Receipt),
            [TenantIdColumn, "ReceiptId"], [TenantIdColumn, nameof(Receipt.Id)], DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(PushDevice), typeof(Membership),
            [TenantIdColumn, nameof(PushDevice.UserId)], null, DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(Membership), typeof(User), [nameof(Membership.UserId)], null, DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(Session), typeof(User), [nameof(Session.UserId)], null, DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(PasswordResetToken), typeof(User), [nameof(PasswordResetToken.UserId)], null, DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(PasswordResetToken), typeof(User), [nameof(PasswordResetToken.IssuedBySupportId)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(LegalAcceptance), typeof(User), [nameof(LegalAcceptance.UserId)], null, DeleteBehavior.Cascade);
        Reference(modelBuilder, typeof(UxEvent), typeof(User), [nameof(UxEvent.UserId)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(SupportGrant), typeof(User), [nameof(SupportGrant.GrantedBy)], null, DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(BusinessAccount), typeof(Bank), [nameof(BusinessAccount.BankId)], null, DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Charge), typeof(User), [nameof(Charge.CreatedBy)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Charge), typeof(User), [nameof(Charge.VoidedBy)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Receipt), typeof(User), [nameof(Receipt.UploadedBy)], null, DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Receipt), typeof(User), [nameof(Receipt.DecidedBy)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Receipt), typeof(Bank), [nameof(Receipt.BankId)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Extraction), typeof(Bank), [nameof(Extraction.DetectedBankId)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Allocation), typeof(User), [nameof(Allocation.CreatedBy)], null, DeleteBehavior.Restrict);
        Reference(modelBuilder, typeof(Allocation), typeof(User), [nameof(Allocation.VoidedBy)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Alert), typeof(User), [nameof(Alert.ResolvedBy)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(AuditEvent), typeof(User), [nameof(AuditEvent.UserId)], null, DeleteBehavior.SetNull);
        Reference(modelBuilder, typeof(Session), typeof(Tenant), [TenantIdColumn], null, DeleteBehavior.SetNull);
    }

    private static void Reference(
        ModelBuilder modelBuilder,
        Type dependent,
        Type principal,
        string[] foreignKey,
        string[]? principalKey,
        DeleteBehavior deleteBehavior)
    {
        var reference = modelBuilder.Entity(dependent)
            .HasOne(principal)
            .WithMany()
            .HasForeignKey(foreignKey)
            .OnDelete(deleteBehavior);

        if (principalKey is not null)
        {
            reference.HasPrincipalKey(principalKey);
        }
    }
}
