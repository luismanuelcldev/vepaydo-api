using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class AppDbContextModelTests
{

    private static readonly (Type Dependent, Type Principal, string[] ForeignKey, string[] PrincipalKey, DeleteBehavior Delete)[]
        ExpectedForeignKeys =
        [

            (typeof(TenantSettings), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Membership), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(PushDevice), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(BusinessAccount), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Customer), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(CustomerAlias), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Charge), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Receipt), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Extraction), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Allocation), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Alert), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(UxEvent), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(SupportGrant), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(AuditEvent), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.Restrict),

            (typeof(CustomerAlias), typeof(Customer), ["TenantId", "CustomerId"], ["TenantId", "Id"], DeleteBehavior.Cascade),
            (typeof(Charge), typeof(Customer), ["TenantId", "CustomerId"], ["TenantId", "Id"], DeleteBehavior.Restrict),
            (typeof(Receipt), typeof(BusinessAccount), ["TenantId", "DestinationAccountId"], ["TenantId", "Id"], DeleteBehavior.Restrict),
            (typeof(Receipt), typeof(Customer), ["TenantId", "CustomerId"], ["TenantId", "Id"], DeleteBehavior.Restrict),
            (typeof(Receipt), typeof(Charge), ["TenantId", "SuggestedChargeId"], ["TenantId", "Id"], DeleteBehavior.Restrict),
            (typeof(Extraction), typeof(Receipt), ["TenantId", "ReceiptId"], ["TenantId", "Id"], DeleteBehavior.Cascade),
            (typeof(Allocation), typeof(Receipt), ["TenantId", "ReceiptId"], ["TenantId", "Id"], DeleteBehavior.Restrict),
            (typeof(Allocation), typeof(Charge), ["TenantId", "ChargeId"], ["TenantId", "Id"], DeleteBehavior.Restrict),
            (typeof(Alert), typeof(Receipt), ["TenantId", "ReceiptId"], ["TenantId", "Id"], DeleteBehavior.Cascade),
            (typeof(PushDevice), typeof(Membership), ["TenantId", "UserId"], ["TenantId", "UserId"], DeleteBehavior.Cascade),

            (typeof(Membership), typeof(User), ["UserId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Session), typeof(User), ["UserId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(Session), typeof(Tenant), ["TenantId"], ["Id"], DeleteBehavior.SetNull),
            (typeof(PasswordResetToken), typeof(User), ["UserId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(PasswordResetToken), typeof(User), ["IssuedBySupportId"], ["Id"], DeleteBehavior.SetNull),
            (typeof(LegalAcceptance), typeof(User), ["UserId"], ["Id"], DeleteBehavior.Cascade),
            (typeof(UxEvent), typeof(User), ["UserId"], ["Id"], DeleteBehavior.SetNull),
            (typeof(SupportGrant), typeof(User), ["GrantedBy"], ["Id"], DeleteBehavior.Restrict),
            (typeof(BusinessAccount), typeof(Bank), ["BankId"], ["Id"], DeleteBehavior.Restrict),
            (typeof(Charge), typeof(User), ["CreatedBy"], ["Id"], DeleteBehavior.SetNull),
            (typeof(Charge), typeof(User), ["VoidedBy"], ["Id"], DeleteBehavior.SetNull),
            (typeof(Receipt), typeof(User), ["UploadedBy"], ["Id"], DeleteBehavior.Restrict),
            (typeof(Receipt), typeof(User), ["DecidedBy"], ["Id"], DeleteBehavior.SetNull),
            (typeof(Receipt), typeof(Bank), ["BankId"], ["Id"], DeleteBehavior.SetNull),
            (typeof(Extraction), typeof(Bank), ["DetectedBankId"], ["Id"], DeleteBehavior.SetNull),
            (typeof(Allocation), typeof(User), ["CreatedBy"], ["Id"], DeleteBehavior.Restrict),
            (typeof(Allocation), typeof(User), ["VoidedBy"], ["Id"], DeleteBehavior.SetNull),
            (typeof(Alert), typeof(User), ["ResolvedBy"], ["Id"], DeleteBehavior.SetNull),
            (typeof(AuditEvent), typeof(User), ["UserId"], ["Id"], DeleteBehavior.SetNull),
        ];
}
