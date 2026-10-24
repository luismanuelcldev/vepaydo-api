using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    private static void ConfigureTenancy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bank>(entity =>
        {
            entity.ToTable("banks").HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedOnAdd();
        });
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users").HasKey(u => u.Id);
            entity.Property(u => u.Email).HasColumnType("citext");
        });
        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("sessions").HasKey(s => s.Id);
            entity.Property(s => s.Ip).HasColumnType("inet");
        });
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens").HasKey(t => t.Id);
            entity.Property(t => t.RequestedIp).HasColumnType("inet");
        });
        modelBuilder.Entity<LegalAcceptance>(entity =>
        {
            entity.ToTable("legal_acceptances").HasKey(l => l.Id);
            entity.Property(l => l.Ip).HasColumnType("inet");
        });
        modelBuilder.Entity<UxEvent>(entity =>
        {
            entity.ToTable("ux_events").HasKey(e => e.Id);
            entity.Property(e => e.Props).HasColumnType("jsonb");
        });
        modelBuilder.Entity<SupportGrant>(entity =>
        {
            entity.ToTable("support_grants").HasKey(g => g.Id);
        });
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants").HasKey(t => t.Id);
        });
        modelBuilder.Entity<TenantSettings>(entity =>
        {
            entity.ToTable("tenant_settings").HasKey(s => s.TenantId);
            entity.Property(s => s.AmountTolerance).HasColumnType("numeric(14,2)");
            entity.Property(s => s.VerifierAmountCap).HasColumnType("numeric(14,2)");
        });
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships").HasKey(m => new { m.TenantId, m.UserId });
        });
        modelBuilder.Entity<PushDevice>(entity =>
        {
            entity.ToTable("push_devices").HasKey(d => d.Id);
        });
    }
}
