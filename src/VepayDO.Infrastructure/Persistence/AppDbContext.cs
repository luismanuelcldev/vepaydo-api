using Microsoft.EntityFrameworkCore;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const string TenantIdColumn = "TenantId";

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
}
