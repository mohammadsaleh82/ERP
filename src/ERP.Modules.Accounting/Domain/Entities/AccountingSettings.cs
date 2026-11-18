using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

public class AccountingSettings : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string DefaultCurrencyCode { get; set; } = "IRR";
    public FiscalCalendarType CalendarType { get; set; } = FiscalCalendarType.Persian;
    public int FiscalYearStartMonth { get; set; } = 1;
    public int FiscalYearStartDay { get; set; } = 1;
    public int DecimalPlaces { get; set; }
    public RoundingMode RoundingMode { get; set; } = RoundingMode.RoundToEven;
    public VoucherNumberingPolicy VoucherNumberingPolicy { get; set; } = VoucherNumberingPolicy.ContinuousByFiscalYear;
    public AllowNegativeBalancesPolicy AllowNegativeBalancesPolicy { get; set; } = AllowNegativeBalancesPolicy.WarningOnly;
    public DocumentApprovalPolicy ApprovalPolicy { get; set; } = DocumentApprovalPolicy.DirectPostingAllowed;

    /// <summary>تفکیک وظایف: ثبت‌کننده سند نمی‌تواند سند خودش را تأیید کند.</summary>
    public bool RequireSeparateApprover { get; set; }

    /// <summary>تفکیک وظایف: تأییدکننده سند نمی‌تواند همان سند را قطعی کند.</summary>
    public bool RequireSeparatePoster { get; set; }

    public Guid? DefaultAccountTemplateId { get; set; }
    public Guid? ActiveFiscalYearId { get; set; }
    public DateOnly? LockDate { get; set; }
    public bool AllowUnbalancedDraft { get; set; } = true;
    public bool AutoNumberingEnabled { get; set; } = true;

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public AccountingSettings()
    {
    }

    public AccountingSettings(
        Guid tenantId,
        Guid companyId,
        string defaultCurrencyCode = "IRR",
        FiscalCalendarType calendarType = FiscalCalendarType.Persian,
        int decimalPlaces = 0)
    {
        if (tenantId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        DefaultCurrencyCode = string.IsNullOrWhiteSpace(defaultCurrencyCode) ? "IRR" : defaultCurrencyCode.Trim().ToUpperInvariant();
        CalendarType = calendarType;
        DecimalPlaces = decimalPlaces;
        FiscalYearStartMonth = 1;
        FiscalYearStartDay = 1;
        RoundingMode = RoundingMode.RoundToEven;
        VoucherNumberingPolicy = VoucherNumberingPolicy.ContinuousByFiscalYear;
        AllowNegativeBalancesPolicy = AllowNegativeBalancesPolicy.WarningOnly;
        ApprovalPolicy = DocumentApprovalPolicy.DirectPostingAllowed;
        AllowUnbalancedDraft = true;
        AutoNumberingEnabled = true;
        Version = 1;
    }

    public void Update(
        string defaultCurrencyCode,
        FiscalCalendarType calendarType,
        int fiscalYearStartMonth,
        int fiscalYearStartDay,
        int decimalPlaces,
        RoundingMode roundingMode,
        VoucherNumberingPolicy voucherNumberingPolicy,
        AllowNegativeBalancesPolicy allowNegativeBalancesPolicy,
        DocumentApprovalPolicy approvalPolicy,
        Guid? defaultAccountTemplateId,
        DateOnly? lockDate,
        bool allowUnbalancedDraft,
        bool autoNumberingEnabled,
        bool requireSeparateApprover = false,
        bool requireSeparatePoster = false)
    {
        if (string.IsNullOrWhiteSpace(defaultCurrencyCode))
            throw new AccountingDomainException("Accounting.EmptyCurrency", "کد واحد پول پیش‌فرض نمی‌تواند خالی باشد.");

        if (decimalPlaces < 0 || decimalPlaces > 4)
            throw new AccountingDomainException("Accounting.InvalidDecimalPlaces", "تعداد ارقام اعشار باید بین ۰ تا ۴ باشد.");

        if (fiscalYearStartMonth < 1 || fiscalYearStartMonth > 12)
            throw new AccountingDomainException("Accounting.InvalidStartMonth", "ماه شروع سال مالی باید بین ۱ تا ۱۲ باشد.");

        if (fiscalYearStartDay < 1 || fiscalYearStartDay > 31)
            throw new AccountingDomainException("Accounting.InvalidStartDay", "روز شروع سال مالی باید بین ۱ تا ۳۱ باشد.");

        DefaultCurrencyCode = defaultCurrencyCode.Trim().ToUpperInvariant();
        CalendarType = calendarType;
        FiscalYearStartMonth = fiscalYearStartMonth;
        FiscalYearStartDay = fiscalYearStartDay;
        DecimalPlaces = decimalPlaces;
        RoundingMode = roundingMode;
        VoucherNumberingPolicy = voucherNumberingPolicy;
        AllowNegativeBalancesPolicy = allowNegativeBalancesPolicy;
        ApprovalPolicy = approvalPolicy;
        DefaultAccountTemplateId = defaultAccountTemplateId;
        LockDate = lockDate;
        AllowUnbalancedDraft = allowUnbalancedDraft;
        AutoNumberingEnabled = autoNumberingEnabled;
        RequireSeparateApprover = requireSeparateApprover;
        RequireSeparatePoster = requireSeparatePoster;
        Version++;
    }

    public bool IsDateLocked(DateOnly date) =>
        LockDate.HasValue && date <= LockDate.Value;
}
