using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    private static void ConfigureBusiness(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessAccount>(entity =>
        {
            entity.ToTable("business_accounts").HasKey(a => a.Id);
            entity.Property(a => a.Last4).HasColumnType("char(4)");
            entity.Property(a => a.Currency).HasColumnType("char(3)");
        });
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers").HasKey(c => c.Id);
        });
        modelBuilder.Entity<CustomerAlias>(entity =>
        {
            entity.ToTable("customer_aliases").HasKey(a => a.Id);
        });
        modelBuilder.Entity<Charge>(entity =>
        {
            entity.ToTable("charges").HasKey(c => c.Id);
            entity.Property(c => c.Amount).HasColumnType("numeric(14,2)");
            entity.Property(c => c.Currency).HasColumnType("char(3)");
        });
        modelBuilder.Entity<ChargeBalanceRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("v_charge_balances");
            entity.Property(r => r.ChargeId).HasColumnName("charge_id");
            entity.Property(r => r.TenantId).HasColumnName("tenant_id");
            entity.Property(r => r.Amount).HasColumnType("numeric(14,2)");
            entity.Property(r => r.Currency).HasColumnType("char(3)");
            entity.Property(r => r.IssuedOn).HasColumnName("issued_on").HasColumnType("date");
            entity.Property(r => r.DueOn).HasColumnName("due_on").HasColumnType("date");
            entity.Property(r => r.AmountApplied).HasColumnName("amount_applied").HasColumnType("numeric(14,2)");
            entity.Property(r => r.Balance).HasColumnType("numeric(14,2)");
            entity.Property(r => r.VoidedAt).HasColumnName("voided_at");
            entity.Property(r => r.VoidedBy).HasColumnName("voided_by");
            entity.Property(r => r.VoidReason).HasColumnName("void_reason");
            entity.Property(r => r.CreatedBy).HasColumnName("created_by");
        });
    }
}
