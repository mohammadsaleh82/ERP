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
}
