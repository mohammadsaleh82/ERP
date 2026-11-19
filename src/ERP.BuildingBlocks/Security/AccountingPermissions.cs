namespace ERP.BuildingBlocks.Security;

/// <summary>
/// ماتریس مجوزهای مستقل حسابداری (PROMPT 09) — هر عملیات حساس مجوز خودش را دارد.
/// مجوزها روی کلیدهای Tenant/Company (هدرهای زمینه) و در صورت کاربرد Branch
/// (ادعای ActiveBranchId) اعمال می‌شوند؛ نام‌های ثابت در Policy Provider به
/// سیاست پویا تبدیل می‌شوند و در تمام کنترلرهای ماژول استفاده می‌شوند.
/// </summary>
public static class AccountingPermissions
{
    // ---- حساب‌ها ------------------------------------------------------------
    public const string ViewAccounts = "accounting:accounts:read";
    public const string CreateAccounts = "accounting:accounts:create";
    public const string UpdateAccounts = "accounting:accounts:update";
    public const string DeactivateAccounts = "accounting:accounts:deactivate";

    // ---- اسناد --------------------------------------------------------------
    public const string ViewVouchers = "accounting:documents:read";
    public const string CreateVouchers = "accounting:documents:create";
    public const string UpdateDraftVouchers = "accounting:documents:update";
    public const string DeleteDraftVouchers = "accounting:documents:delete";
    public const string SubmitVouchers = "accounting:documents:submit";
    public const string ApproveVouchers = "accounting:documents:approve";
    public const string RejectVouchers = "accounting:documents:reject";
    public const string PostVouchers = "accounting:documents:post";
    public const string ReverseVouchers = "accounting:documents:reverse";

    // ---- دفتر و گزارش‌ها -----------------------------------------------------
    public const string ViewLedger = "accounting:ledger:read";
    public const string ViewFinancialReports = "accounting:reports:read";

    // ---- سال/دوره مالی -------------------------------------------------------
    public const string CreateFiscalYear = "accounting:fiscal-years:create";
    public const string CloseFiscalPeriod = "accounting:periods:close";
    public const string CloseFiscalYear = "accounting:fiscalyear:close";
    public const string ReopenFiscalYear = "accounting:fiscalyear:reopen";

    // ---- تنظیمات --------------------------------------------------------------
    public const string ManageAccountingSettings = "accounting:settings:update";

    // ---- عملیات کمکی خارج از ماتریس اصلی (حساب بدون گردش) ----------------------
    public const string DeleteAccounts = "accounting:accounts:delete";

    /// <summary>ماتریس کامل ۲۰ مجوز مستقل — برای تست پوشش و مستندسازی.</summary>
    public static readonly string[] Matrix =
    [
        ViewAccounts, CreateAccounts, UpdateAccounts, DeactivateAccounts,
        ViewVouchers, CreateVouchers, UpdateDraftVouchers, DeleteDraftVouchers,
        SubmitVouchers, ApproveVouchers, RejectVouchers, PostVouchers, ReverseVouchers,
        ViewLedger, ViewFinancialReports,
        CreateFiscalYear, CloseFiscalPeriod, CloseFiscalYear, ReopenFiscalYear,
        ManageAccountingSettings
    ];
}

/// <summary>تامین شناسه همبستگی برای Audit Trail (هدر X-Correlation-Id یا TraceIdentifier).</summary>
public interface ICorrelationIdProvider
{
    string GetCorrelationId();
}

