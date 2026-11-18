namespace ERP.Modules.Accounting.Application.Services.Audit;

/// <summary>کدهای استاندارد عملیات ممیزی.</summary>
public static class AuditTrailActions
{
    public const string VoucherCreated = "Voucher.Created";
    public const string VoucherDraftUpdated = "Voucher.DraftUpdated";
    public const string VoucherDraftDeleted = "Voucher.DraftDeleted";
    public const string VoucherSubmitted = "Voucher.Submitted";
    public const string VoucherApproved = "Voucher.Approved";
    public const string VoucherRejected = "Voucher.Rejected";
    public const string VoucherPosted = "Voucher.Posted";
    public const string VoucherReversed = "Voucher.Reversed";
    public const string AccountCreated = "Account.Created";
    public const string AccountUpdated = "Account.Updated";
    public const string AccountDeactivated = "Account.Deactivated";
    public const string SettingsUpdated = "Settings.Updated";
    public const string PeriodClosed = "Period.Closed";
    public const string FiscalYearClosed = "FiscalYear.Closed";
    public const string FiscalYearReopened = "FiscalYear.Reopened";
}
