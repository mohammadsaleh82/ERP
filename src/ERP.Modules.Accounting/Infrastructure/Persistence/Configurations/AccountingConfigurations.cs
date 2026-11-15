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
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.IdempotencyKey });
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
