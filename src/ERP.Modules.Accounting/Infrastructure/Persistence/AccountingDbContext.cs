using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Infrastructure.Persistence;

public class AccountingDbContext : BaseDbContext
{
    public const string Schema = "acc";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Accounting";

    public DbSet<AccountingSettings> AccountingSettings => Set<AccountingSettings>();
    public DbSet<AccountingSettingsHistory> SettingsHistory => Set<AccountingSettingsHistory>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<VoucherSequence> VoucherSequences => Set<VoucherSequence>();
    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountTemplate> AccountTemplates => Set<AccountTemplate>();
    public DbSet<AccountTemplateLevel> AccountTemplateLevels => Set<AccountTemplateLevel>();
    public DbSet<AccountTemplateItem> AccountTemplateItems => Set<AccountTemplateItem>();
    public DbSet<AccountingDocument> Documents => Set<AccountingDocument>();
    public DbSet<AccountingDocumentLine> DocumentLines => Set<AccountingDocumentLine>();
    public DbSet<GeneralLedgerEntry> GeneralLedgerEntries => Set<GeneralLedgerEntry>();
    public DbSet<DocumentAuditLog> DocumentAuditLogs => Set<DocumentAuditLog>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<FinancialDimension> FinancialDimensions => Set<FinancialDimension>();
    public DbSet<AccountDimensionPolicy> AccountDimensionPolicies => Set<AccountDimensionPolicy>();
    public DbSet<DimensionCombinationRule> DimensionCombinationRules => Set<DimensionCombinationRule>();
    public DbSet<DocumentLineDimension> DocumentLineDimensions => Set<DocumentLineDimension>();
    public DbSet<GeneralLedgerEntryDimension> GeneralLedgerEntryDimensions => Set<GeneralLedgerEntryDimension>();
    public DbSet<FiscalYearClosing> FiscalYearClosings => Set<FiscalYearClosing>();
    public DbSet<FiscalYearClosingStep> FiscalYearClosingSteps => Set<FiscalYearClosingStep>();
    public DbSet<FiscalYearReopening> FiscalYearReopenings => Set<FiscalYearReopening>();

    public AccountingDbContext(
        DbContextOptions<AccountingDbContext> options,
        ITenantContext? tenantContext = null,
        ICurrentUserContext? currentUserContext = null,
        IClock? clock = null)
        : base(options, tenantContext, currentUserContext, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
    }

    /// <summary>
    /// گارد تغییرناپذیری Posted Ledger: سطرهای دفتر کل پس از ثبت قطعی هیچ‌گاه اجازه
    /// UPDATE یا DELETE ندارند؛ اصلاح تنها با سند معکوس (ایجاد سطرهای جدید) معتبر است.
    /// این گارد در همان لایه Persistence جلوی هر مسیر نوشتن ناخواسته را می‌گیرد.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureLedgerEntriesImmutable();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureLedgerEntriesImmutable();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges()
    {
        EnsureLedgerEntriesImmutable();
        return base.SaveChanges();
    }

    private void EnsureLedgerEntriesImmutable()
    {
        var tampered = ChangeTracker.Entries<GeneralLedgerEntry>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (tampered.Count > 0)
        {
            throw new ERP.Modules.Accounting.Domain.Exceptions.AccountingDomainException(
                "Accounting.PostedLedgerImmutable",
                $"سطرهای دفتر کل تغییرناپذیرند؛ تلاش برای ویرایش یا حذف {tampered.Count} سطر دفتر کل مسدود شد. " +
                "اصلاح گردش مالی تنها از طریق سند معکوس (Storno) به‌صورت سطرهای جدید مجاز است.");
        }
    }
}
