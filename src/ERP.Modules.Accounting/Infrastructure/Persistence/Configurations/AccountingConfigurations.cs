using ERP.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class AccountingSettingsConfiguration : IEntityTypeConfiguration<AccountingSettings>
{
    public void Configure(EntityTypeBuilder<AccountingSettings> builder)
    {
        builder.ToTable("acc_accounting_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DefaultCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(s => s.DecimalPlaces).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken();

        builder.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique();
    }
}

public class AccountingSettingsHistoryConfiguration : IEntityTypeConfiguration<AccountingSettingsHistory>
{
    public void Configure(EntityTypeBuilder<AccountingSettingsHistory> builder)
    {
        builder.ToTable("acc_accounting_settings_history");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.DefaultCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(h => h.ChangedBy).HasMaxLength(100);
        builder.Property(h => h.ChangeReason).HasMaxLength(250);

        builder.HasIndex(h => new { h.TenantId, h.CompanyId, h.SettingsId, h.Version });
    }
}

public class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("acc_fiscal_years");
        builder.HasKey(y => y.Id);

        builder.Property(y => y.Name).HasMaxLength(100).IsRequired();
        builder.Property(y => y.ClosedBy).HasMaxLength(100);
        builder.Property(y => y.Version).IsConcurrencyToken();

        builder.HasIndex(y => new { y.TenantId, y.CompanyId, y.YearNumber }).IsUnique();
        builder.HasIndex(y => new { y.TenantId, y.CompanyId, y.StartDate, y.EndDate });

        builder.HasMany(y => y.Periods)
            .WithOne(p => p.FiscalYear)
            .HasForeignKey(p => p.FiscalYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        builder.ToTable("acc_fiscal_periods");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.CompanyId, p.FiscalYearId, p.PeriodNumber }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.CompanyId, p.FiscalYearId, p.StartDate, p.EndDate });
    }
}

public class VoucherSequenceConfiguration : IEntityTypeConfiguration<VoucherSequence>
{
    public void Configure(EntityTypeBuilder<VoucherSequence> builder)
    {
        builder.ToTable("acc_voucher_sequences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SequenceType).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Prefix).HasMaxLength(20);
        builder.Property(s => s.FormatPattern).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken();

        builder.HasIndex(s => new { s.TenantId, s.CompanyId, s.FiscalYearId, s.SequenceType, s.PeriodId }).IsUnique();
    }
}

public class AccountGroupConfiguration : IEntityTypeConfiguration<AccountGroup>
{
    public void Configure(EntityTypeBuilder<AccountGroup> builder)
    {
        builder.ToTable("acc_account_groups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Code).HasMaxLength(10).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(g => new { g.TenantId, g.CompanyId, g.Code }).IsUnique();
    }
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("acc_accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Code).HasMaxLength(20).IsRequired();
        builder.Property(a => a.FullCode).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(500);

        builder.Property(a => a.Version).IsConcurrencyToken();

        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.FullCode }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.Code });
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.ParentAccountId });
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.Level });
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.IsActive, a.AllowPosting });

        builder.HasOne(a => a.AccountGroup)
            .WithMany(g => g.Accounts)
            .HasForeignKey(a => a.AccountGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ParentAccount)
            .WithMany(a => a.SubAccounts)
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountTemplateConfiguration : IEntityTypeConfiguration<AccountTemplate>
{
    public void Configure(EntityTypeBuilder<AccountTemplate> builder)
    {
        builder.ToTable("acc_account_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);

        builder.HasMany(t => t.Levels)
            .WithOne(l => l.Template)
            .HasForeignKey(l => l.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Items)
            .WithOne(i => i.Template)
            .HasForeignKey(i => i.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountTemplateLevelConfiguration : IEntityTypeConfiguration<AccountTemplateLevel>
{
    public void Configure(EntityTypeBuilder<AccountTemplateLevel> builder)
    {
        builder.ToTable("acc_account_template_levels");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name).HasMaxLength(50).IsRequired();

        builder.HasIndex(l => new { l.TemplateId, l.Level }).IsUnique();
    }
}

public class AccountTemplateItemConfiguration : IEntityTypeConfiguration<AccountTemplateItem>
{
    public void Configure(EntityTypeBuilder<AccountTemplateItem> builder)
    {
        builder.ToTable("acc_account_template_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Code).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ParentCode).HasMaxLength(20);
        builder.Property(i => i.Description).HasMaxLength(500);

        builder.HasIndex(i => new { i.TemplateId, i.Code }).IsUnique();
        builder.HasIndex(i => new { i.TemplateId, i.ParentCode });
    }
}

public class AccountingDocumentConfiguration : IEntityTypeConfiguration<AccountingDocument>
{
    public void Configure(EntityTypeBuilder<AccountingDocument> builder)
    {
        builder.ToTable("acc_documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Description).HasMaxLength(500).IsRequired();
        builder.Property(d => d.ReferenceNumber).HasMaxLength(100);
        builder.Property(d => d.SourceModule).HasMaxLength(50);
        builder.Property(d => d.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(d => d.IdempotencyKey).HasMaxLength(128);

        builder.Property(d => d.TotalDebit).HasPrecision(28, 4);
        builder.Property(d => d.TotalCredit).HasPrecision(28, 4);

        // Optimistic concurrency token
        builder.Property(d => d.Version).IsConcurrencyToken();

        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.FiscalYearId, d.DocumentNumber }).IsUnique();
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.DocumentDate, d.Status });
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.IdempotencyKey }).IsUnique(); // کلید ایدمپوتنسی یکتا در سطح شرکت (مقادیر NULL متعدد مجاز است)
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.SourceModule, d.SourceDocumentId });

        builder.HasMany(d => d.Lines)
            .WithOne(l => l.Document)
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountingDocumentLineConfiguration : IEntityTypeConfiguration<AccountingDocumentLine>
{
    public void Configure(EntityTypeBuilder<AccountingDocumentLine> builder)
    {
        builder.ToTable("acc_document_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.ForeignCurrencyCode).HasMaxLength(3);

        builder.Property(l => l.DebitAmount).HasPrecision(28, 4);
        builder.Property(l => l.CreditAmount).HasPrecision(28, 4);
        builder.Property(l => l.ForeignCurrencyAmount).HasPrecision(28, 4);
        builder.Property(l => l.ExchangeRate).HasPrecision(18, 6);

        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.DocumentId, l.LineNumber }).IsUnique();
        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.AccountId, l.DocumentId });
        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.CostCenterId });
        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.ProjectId });
        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.DetailAccountId });

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GeneralLedgerEntryConfiguration : IEntityTypeConfiguration<GeneralLedgerEntry>
{
    public void Configure(EntityTypeBuilder<GeneralLedgerEntry> builder)
    {
        builder.ToTable("acc_gl_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ForeignCurrencyCode).HasMaxLength(3);
        builder.Property(e => e.SourceModule).HasMaxLength(50);
        builder.Property(e => e.PostedBy).HasMaxLength(100).IsRequired();

        builder.Property(e => e.DebitAmount).HasPrecision(28, 4);
        builder.Property(e => e.CreditAmount).HasPrecision(28, 4);
        builder.Property(e => e.ForeignCurrencyAmount).HasPrecision(28, 4);
        builder.Property(e => e.ExchangeRate).HasPrecision(18, 6);

        // گارد پایگاه‌داده‌ای در برابر ثبت دوباره (Double Posting): هر ردیف سند تنها یک سطر دفتر کل می‌تواند داشته باشد
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.DocumentId, e.DocumentLineId }).IsUnique();

        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.FiscalYearId, e.FiscalPeriodId });
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.AccountId, e.EntryDate });
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.DocumentId });
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.SourceModule, e.SourceDocumentId });
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.BranchId, e.EntryDate }); // فیلتر شعبه در گزارش‌های دفتر
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.EntryDate, e.DocumentNumber }); // ترتیب دفتر روزنامه
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.FiscalYearId, e.EntryDate }); // گزارش‌های بازه تاریخی در سال مالی
    }
}

public class DocumentAuditLogConfiguration : IEntityTypeConfiguration<DocumentAuditLog>
{
    public void Configure(EntityTypeBuilder<DocumentAuditLog> builder)
    {
        builder.ToTable("acc_document_audit_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Details).HasMaxLength(1000);
        builder.Property(l => l.PerformedBy).HasMaxLength(100).IsRequired();

        // رکوردهای ممیزی بدون وابستگی FK به سند نگهداری می‌شوند تا پس از حذف پیش‌نویس نیز باقی بمانند
        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.DocumentId, l.PerformedAtUtc });
        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.Action });
    }
}

public class AccountBalanceSnapshotConfiguration : IEntityTypeConfiguration<AccountBalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<AccountBalanceSnapshot> builder)
    {
        builder.ToTable("acc_account_balance_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PeriodDebit).HasPrecision(28, 4);
        builder.Property(s => s.PeriodCredit).HasPrecision(28, 4);
        builder.Property(s => s.Version).IsConcurrencyToken();

        // یک ردیف به ازای هر (شرکت، دوره مالی، حساب)
        builder.HasIndex(s => new { s.TenantId, s.CompanyId, s.FiscalPeriodId, s.AccountId }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.CompanyId, s.FiscalYearId, s.AccountId });
    }
}

public class FinancialDimensionConfiguration : IEntityTypeConfiguration<FinancialDimension>
{
    public void Configure(EntityTypeBuilder<FinancialDimension> builder)
    {
        builder.ToTable("acc_financial_dimensions");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Code).HasMaxLength(30).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.OwnerModule).HasMaxLength(50);
        builder.Property(d => d.Version).IsConcurrencyToken();

        // کد بعد مالی برای هر نوع، در هر شرکت یکتاست
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.DimensionType, d.Code }).IsUnique();
        // جستجوی مرجع خارجی ماژول مالک
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.DimensionType, d.ExternalReferenceId });
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.DimensionType, d.IsActive });
    }
}

public class AccountDimensionPolicyConfiguration : IEntityTypeConfiguration<AccountDimensionPolicy>
{
    public void Configure(EntityTypeBuilder<AccountDimensionPolicy> builder)
    {
        builder.ToTable("acc_account_dimension_policies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Version).IsConcurrencyToken();

        // حداکثر یک سیاست به ازای هر (حساب، نوع بعد)
        builder.HasIndex(p => new { p.TenantId, p.CompanyId, p.AccountId, p.DimensionType }).IsUnique();
    }
}

public class DimensionCombinationRuleConfiguration : IEntityTypeConfiguration<DimensionCombinationRule>
{
    public void Configure(EntityTypeBuilder<DimensionCombinationRule> builder)
    {
        builder.ToTable("acc_dimension_combination_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Description).HasMaxLength(300);
        builder.Property(r => r.Version).IsConcurrencyToken();

        builder.HasIndex(r => new { r.TenantId, r.CompanyId });
    }
}

public class DocumentLineDimensionConfiguration : IEntityTypeConfiguration<DocumentLineDimension>
{
    public void Configure(EntityTypeBuilder<DocumentLineDimension> builder)
    {
        builder.ToTable("acc_document_line_dimensions");
        builder.HasKey(a => a.Id);

        // یک مقدار به ازای هر نوع بعد در هر ردیف
        builder.HasIndex(a => new { a.DocumentLineId, a.DimensionType }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.DimensionType, a.DimensionId });

        builder.HasOne<AccountingDocumentLine>()
            .WithMany(l => l.Dimensions)
            .HasForeignKey(a => a.DocumentLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GeneralLedgerEntryDimensionConfiguration : IEntityTypeConfiguration<GeneralLedgerEntryDimension>
{
    public void Configure(EntityTypeBuilder<GeneralLedgerEntryDimension> builder)
    {
        builder.ToTable("acc_gl_entry_dimensions");
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.DimensionType, a.DimensionId }); // محور گزارش تحلیلی
        builder.HasIndex(a => new { a.GeneralLedgerEntryId, a.DimensionType }).IsUnique();

        builder.HasOne<GeneralLedgerEntry>()
            .WithMany()
            .HasForeignKey(a => a.GeneralLedgerEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


public class FiscalYearClosingConfiguration : IEntityTypeConfiguration<FiscalYearClosing>
{
    public void Configure(EntityTypeBuilder<FiscalYearClosing> builder)
    {
        builder.ToTable("acc_fiscal_year_closings");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FailedReason).HasMaxLength(2000);
        builder.Property(c => c.StartedBy).HasMaxLength(120);
        builder.Property(c => c.ClosedBy).HasMaxLength(120);
        builder.Property(c => c.Version).IsConcurrencyToken();

        // یک گردش‌کار اختتامیه به ازای هر سال مالی شرکت
        builder.HasIndex(c => new { c.TenantId, c.CompanyId, c.FiscalYearId }).IsUnique();

        builder.HasMany(c => c.Steps)
            .WithOne()
            .HasForeignKey(s => s.FiscalYearClosingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FiscalYearClosingStepConfiguration : IEntityTypeConfiguration<FiscalYearClosingStep>
{
    public void Configure(EntityTypeBuilder<FiscalYearClosingStep> builder)
    {
        builder.ToTable("acc_fiscal_year_closing_steps");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Output).HasMaxLength(2000);

        builder.HasIndex(s => new { s.FiscalYearClosingId, s.StepNumber });
    }
}

public class FiscalYearReopeningConfiguration : IEntityTypeConfiguration<FiscalYearReopening>
{
    public void Configure(EntityTypeBuilder<FiscalYearReopening> builder)
    {
        builder.ToTable("acc_fiscal_year_reopenings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.RequestedBy).HasMaxLength(120).IsRequired();
        builder.Property(r => r.ApprovedBy).HasMaxLength(120).IsRequired();
        builder.Property(r => r.NextOpeningImpact).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ChangesSummary).HasMaxLength(2000);
        builder.Property(r => r.Version).IsConcurrencyToken();

        builder.HasIndex(r => new { r.TenantId, r.CompanyId, r.FiscalYearId });
    }
}


public class ReportingClassificationConfiguration : IEntityTypeConfiguration<ReportingClassification>
{
    public void Configure(EntityTypeBuilder<ReportingClassification> builder)
    {
        builder.ToTable("acc_reporting_classifications");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.RootCode).HasMaxLength(2).IsRequired();
        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.HasIndex(c => new { c.TenantId, c.CompanyId, c.RootCode }).IsUnique();
    }
}

public class CashFlowClassificationConfiguration : IEntityTypeConfiguration<CashFlowClassification>
{
    public void Configure(EntityTypeBuilder<CashFlowClassification> builder)
    {
        builder.ToTable("acc_cash_flow_classifications");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.HasIndex(c => new { c.TenantId, c.CompanyId, c.AccountId }).IsUnique();
    }
}
