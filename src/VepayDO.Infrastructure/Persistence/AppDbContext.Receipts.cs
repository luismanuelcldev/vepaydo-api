using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    private static void ConfigureReceipts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("receipts").HasKey(r => r.Id);
            entity.Property(r => r.Amount).HasColumnType("numeric(14,2)");
            entity.Property(r => r.Currency).HasColumnType("char(3)");
            entity.Property(r => r.DestinationLast4).HasColumnType("char(4)");
            entity.Property(r => r.MatchScore).HasColumnType("numeric(4,3)");
            entity.Property(r => r.MatchSignals).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.ToTable("processing_jobs").HasKey(j => j.Id);
        });
        modelBuilder.Entity<Extraction>(entity =>
        {
            entity.ToTable("extractions").HasKey(e => e.Id);
            entity.Property(e => e.Fields).HasColumnType("jsonb");
            entity.Property(e => e.Raw).HasColumnType("jsonb");
            entity.Property(e => e.MinConfidence).HasColumnType("numeric(4,3)");
        });
        modelBuilder.Entity<Allocation>(entity =>
        {
            entity.ToTable("allocations").HasKey(a => a.Id);
            entity.Property(a => a.AmountApplied).HasColumnType("numeric(14,2)");
        });
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("alerts").HasKey(a => a.Id);
            entity.Property(a => a.Details).HasColumnType("jsonb");
        });
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events").HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Before).HasColumnType("jsonb");
            entity.Property(e => e.After).HasColumnType("jsonb");
            entity.Property(e => e.Ip).HasColumnType("inet");
        });
    }
}
