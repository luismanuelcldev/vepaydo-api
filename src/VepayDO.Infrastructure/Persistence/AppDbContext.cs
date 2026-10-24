using System.Text;
using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence;

/// <summary>
/// EF Core model over the hand-written PostgreSQL schema. The schema itself is
/// owned by <c>db/migrations</c> (RLS, triggers, views, grants); this context only
/// maps tables, columns, keys, and PostgreSQL-specific column types for runtime use.
/// It is never used to generate migrations.
///
/// PostgreSQL enum types are mapped through <c>AppDataSource.ConfigureEnums</c> on the
/// <c>UseNpgsql</c> options (required since EF 9), not declared here.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const string TenantIdColumn = "TenantId";

    /// <summary>Entities whose table carries a <c>tenant_id</c> foreign key.</summary>
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

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    public DbSet<UxEvent> UxEvents => Set<UxEvent>();

    public DbSet<SupportGrant> SupportGrants => Set<SupportGrant>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    public DbSet<BusinessAccount> BusinessAccounts => Set<BusinessAccount>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAlias> CustomerAliases => Set<CustomerAlias>();

    public DbSet<Charge> Charges => Set<Charge>();

    public DbSet<ChargeBalanceRow> ChargeBalances => Set<ChargeBalanceRow>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    public DbSet<Extraction> Extractions => Set<Extraction>();

    public DbSet<Allocation> Allocations => Set<Allocation>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureTenancy(modelBuilder);
        ConfigureBusiness(modelBuilder);
        ConfigureReceipts(modelBuilder);
        ConfigureRelationships(modelBuilder);

        ApplySnakeCaseColumnNames(modelBuilder);
    }

    /// <summary>
    /// The entities deliberately expose no navigation properties, so EF cannot infer
    /// any relationship by convention. Without them SaveChanges would order inserts
    /// arbitrarily and a child row could be written before its parent. These calls
    /// mirror the foreign keys declared in <c>db/migrations</c> exactly: same columns,
    /// same principal key (including the composite <c>(tenant_id, id)</c> keys), and
    /// the same referential action.
    /// </summary>
    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // One foreign key per tenant-scoped table (*_tenant_id_fkey).
        foreach (var type in TenantScopedEntityTypes)
        {
            Reference(modelBuilder, type, typeof(Tenant), [TenantIdColumn], null, DeleteBehavior.Cascade);
        }

        // audit_events.tenant_id is the single RESTRICT tenant reference in the schema.
        Reference(modelBuilder, typeof(AuditEvent), typeof(Tenant), [TenantIdColumn], null, DeleteBehavior.Restrict);

        // Composite (tenant_id, *_id) references onto the parent's (tenant_id, id) key.
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

        // push_devices -> memberships(tenant_id, user_id): the memberships primary key.
        Reference(modelBuilder, typeof(PushDevice), typeof(Membership),
            [TenantIdColumn, nameof(PushDevice.UserId)], null, DeleteBehavior.Cascade);

        // Single-column references onto the principal primary key.
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

        // sessions.tenant_id is the tenant the session selected. The column is
        // nullable and the constraint is ON DELETE SET NULL, so deleting a tenant
        // unbinds the session instead of removing the user's login.
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

        // v_charge_balances is defined in db/migrations and is read-only here.
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

        // The processing queue carries no business data, so it has no tenant
        // foreign key: the worker discovers the jobs of every tenant (M5.4).
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

    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                var previousIsLowerOrDigit = i > 0 && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]));
                var nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                if (i > 0 && (previousIsLowerOrDigit || nextIsLower))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
