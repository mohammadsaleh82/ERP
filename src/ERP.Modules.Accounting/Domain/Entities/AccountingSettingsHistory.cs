using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Domain.Entities;

public class AccountingSettingsHistory : Entity, ITenantScoped, ICompanyScoped
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SettingsId { get; set; }

    public uint Version { get; set; }
    public string DefaultCurrencyCode { get; set; } = string.Empty;
    public FiscalCalendarType CalendarType { get; set; }
    public int FiscalYearStartMonth { get; set; }
    public int FiscalYearStartDay { get; set; }
    public int DecimalPlaces { get; set; }
    public RoundingMode RoundingMode { get; set; }
    public VoucherNumberingPolicy VoucherNumberingPolicy { get; set; }
    public AllowNegativeBalancesPolicy AllowNegativeBalancesPolicy { get; set; }
    public DocumentApprovalPolicy ApprovalPolicy { get; set; }
    public DateOnly? LockDate { get; set; }
    public string? ChangedBy { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string? ChangeReason { get; set; }

    public AccountingSettingsHistory()
    {
    }

    public static AccountingSettingsHistory CreateSnapshot(
        AccountingSettings settings,
        string? changedBy,
        DateTimeOffset changedAtUtc,
        string? changeReason = null)
    {
        return new AccountingSettingsHistory
        {
            Id = Guid.NewGuid(),
            TenantId = settings.TenantId,
            CompanyId = settings.CompanyId,
            SettingsId = settings.Id,
            Version = settings.Version,
            DefaultCurrencyCode = settings.DefaultCurrencyCode,
            CalendarType = settings.CalendarType,
            FiscalYearStartMonth = settings.FiscalYearStartMonth,
            FiscalYearStartDay = settings.FiscalYearStartDay,
            DecimalPlaces = settings.DecimalPlaces,
            RoundingMode = settings.RoundingMode,
            VoucherNumberingPolicy = settings.VoucherNumberingPolicy,
            AllowNegativeBalancesPolicy = settings.AllowNegativeBalancesPolicy,
            ApprovalPolicy = settings.ApprovalPolicy,
            LockDate = settings.LockDate,
            ChangedBy = changedBy,
            ChangedAtUtc = changedAtUtc,
            ChangeReason = changeReason
        };
    }
}
