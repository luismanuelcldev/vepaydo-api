using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class AppDbContextModelTests
{

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
}
