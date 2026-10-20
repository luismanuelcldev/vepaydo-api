using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

/// <summary>
/// Model-level checks that do not need a database. The entities expose no navigation
/// properties, so <c>AppDbContext.ConfigureRelationships</c> has to declare every
/// foreign key by hand; without them SaveChanges would order inserts arbitrarily,
/// cross-parent rows could be written, and the required/optional nullability of each
/// reference would be wrong. This is a complete inventory check: nothing missing,
/// nothing invented.
/// </summary>
public sealed class AppDbContextModelTests
{
    /// <summary>Dependent, principal, foreign-key columns, principal-key columns, referential action.</summary>
    private static readonly (Type Dependent, Type Principal, string[] ForeignKey, string[] PrincipalKey, DeleteBehavior Delete)[]
        ExpectedForeignKeys =
        [
            // Tenant ownership (*_tenant_id_fkey).
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

            // Composite (tenant_id, *_id) onto the parent's (tenant_id, id) key.
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

            // Single-column references onto the principal primary key.
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

    [Fact]
    public void The_model_declares_exactly_the_foreign_keys_of_the_schema()
    {
        using var db = CreateModel();

        var actual = new List<(string Dependent, string Principal, string[] ForeignKey, string[] PrincipalKey, DeleteBehavior Delete)>();

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                actual.Add((
                    entityType.ClrType.FullName ?? entityType.Name,
                    foreignKey.PrincipalEntityType.ClrType.FullName ?? foreignKey.PrincipalEntityType.Name,
                    [.. foreignKey.Properties.Select(p => p.Name)],
                    [.. foreignKey.PrincipalKey.Properties.Select(p => p.Name)],
                    foreignKey.DeleteBehavior));
            }
        }

        var expected = ExpectedForeignKeys
            .Select(e => (
                e.Dependent.FullName ?? e.Dependent.Name,
                e.Principal.FullName ?? e.Principal.Name,
                e.ForeignKey,
                e.PrincipalKey,
                e.Delete))
            .ToList();

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Tenant_scoped_entities_declare_the_tenant_key()
    {
        using var db = CreateModel();

        foreach (var type in new[]
                 {
                     typeof(Receipt), typeof(Charge), typeof(Alert), typeof(Allocation),
                     typeof(Extraction), typeof(Customer), typeof(BusinessAccount),
                 })
        {
            var entity = db.Model.FindEntityType(type);
            entity.Should().NotBeNull(type.Name);

            entity!.GetForeignKeys().Should().Contain(
                fk => fk.PrincipalEntityType.ClrType == typeof(Tenant),
                $"{type.Name} must declare the tenant foreign key");
        }
    }

    [Fact]
    public void Non_nullable_foreign_keys_produce_required_relationships()
    {
        using var db = CreateModel();

        var required = db.Model.FindEntityType(typeof(Alert))!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Receipt));

        required.IsRequired.Should().BeTrue(
            "alerts.receipt_id is NOT NULL, so the relationship must be required");
    }

    private static AppDbContext CreateModel()
    {
        var dataSource = AppDataSource.Build(
            "Host=localhost;Port=5432;Database=model;Username=model;Password=model");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
            .Options;

        return new AppDbContext(options);
    }
}
