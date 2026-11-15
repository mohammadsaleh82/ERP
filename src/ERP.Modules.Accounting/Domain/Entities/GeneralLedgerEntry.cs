using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// سطر دفتر کل (General Ledger Entry).
/// هر سطر در لحظه قطعی‌سازی سند، با ارجاع مستقیم به سند و ردیف مبدأ ایجاد می‌شود تا
/// هیچ مانده مالی بدون منبع قابل ردیابی ایجاد نشود.
/// این رکورد پس از ایجاد تغییرناپذیر است و فقط با سند معکوس اصلاح می‌شود.
/// </summary>
public class GeneralLedgerEntry : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public Guid FiscalPeriodId { get; set; }

    public Guid DocumentId { get; set; }
    public Guid DocumentLineId { get; set; }
    public long DocumentNumber { get; set; }
    public int LineNumber { get; set; }

    public Guid AccountId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;

    public DebitCredit Side { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    public string CurrencyCode { get; set; } = "IRR";
    public string? ForeignCurrencyCode { get; set; }
    public decimal? ForeignCurrencyAmount { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? SourceModule { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public bool IsReversal { get; set; }

    public string PostedBy { get; set; } = string.Empty;
    public DateTimeOffset PostedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public GeneralLedgerEntry()
    {
    }

    public static GeneralLedgerEntry FromDocumentLine(
        AccountingDocument document,
        AccountingDocumentLine line,
        Guid fiscalPeriodId,
        string postedBy,
        DateTimeOffset postedAtUtc,
        bool isReversal = false)
    {
        if (document.Status != DocumentStatus.Posted)
            throw new AccountingDomainException("Accounting.GlEntryFromUnpostedDocument",
                "ایجاد سطر دفتر کل تنها برای اسناد قطعی‌شده مجاز است.");

        if (line.DebitAmount > 0m && line.CreditAmount > 0m)
            throw new AccountingDomainException("Accounting.BothSidesPositive",
                $"ردیف {line.LineNumber} سند '{document.DocumentNumber}' هم‌زمان مبلغ بدهکار و بستانکار مثبت دارد.");

        return new GeneralLedgerEntry
        {
            TenantId = document.TenantId,
            CompanyId = document.CompanyId,
            FiscalYearId = document.FiscalYearId,
            FiscalPeriodId = fiscalPeriodId,
            DocumentId = document.Id,
            DocumentLineId = line.Id,
            DocumentNumber = document.DocumentNumber,
            LineNumber = line.LineNumber,
            AccountId = line.AccountId,
            EntryDate = document.DocumentDate,
            Description = line.Description,
            Side = line.DebitCredit,
            DebitAmount = line.DebitAmount,
            CreditAmount = line.CreditAmount,
            CurrencyCode = document.CurrencyCode,
            ForeignCurrencyCode = line.ForeignCurrencyCode,
            ForeignCurrencyAmount = line.ForeignCurrencyAmount,
            ExchangeRate = line.ExchangeRate,
            SourceModule = document.SourceModule,
            SourceDocumentId = document.SourceDocumentId,
            IsReversal = isReversal,
            PostedBy = postedBy,
            PostedAtUtc = postedAtUtc
        };
    }
}
