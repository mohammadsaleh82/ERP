namespace ERP.Modules.Accounting.Domain.Enums;

public enum DebitCredit
{
    Debit = 1,  // بدهکار
    Credit = 2  // بستانکار
}

public static class DebitCreditExtensions
{
    public static int ToSignedMultiplier(this DebitCredit debitCredit) =>
        debitCredit == DebitCredit.Debit ? 1 : -1;

    public static DebitCredit Opposite(this DebitCredit debitCredit) =>
        debitCredit == DebitCredit.Debit ? DebitCredit.Credit : DebitCredit.Debit;
}

public enum DocumentStatus
{
    Draft = 1,            // پیش‌نویس (قابل ویرایش و حذف فیزیکی)
    PendingApproval = 2,  // در انتظار تأیید (ارسال‌شده به جریان تأیید)
    Reviewed = 3,         // بررسی شده (مرحله میانی سیاست تأیید سه‌مرحله‌ای)
    Approved = 4,         // تایید شده و آماده ثبت قطعی
    Posted = 5,           // ثبت قطعی / دائم (غیرقابل ویرایش؛ اصلاح فقط با سند معکوس یا تعدیل)
    Reversed = 6          // باطل‌شده با سند معکوس
}

public enum DocumentType
{
    General = 1,     // سند عمومی / عادی
    Opening = 2,     // سند افتتاحیه
    Closing = 3,     // سند اختتامیه و بستن حساب‌ها
    Adjustment = 4,  // سند تعدیلات پایان دوره
    Reversal = 5     // سند معکوس (Storno)
}

public enum AccountNature
{
    Asset = 1,       // دارایی
    Liability = 2,   // بدهی
    Equity = 3,      // حقوق صاحبان سهام / سرمایه
    Revenue = 4,     // درآمد
    Expense = 5      // هزینه
}

public enum NormalBalance
{
    Debit = 1,       // بدهکار
    Credit = 2       // بستانکار
}

public static class AccountNatureExtensions
{
    public static NormalBalance GetDefaultNormalBalance(this AccountNature nature) =>
        nature switch
        {
            AccountNature.Asset => NormalBalance.Debit,
            AccountNature.Expense => NormalBalance.Debit,
            AccountNature.Liability => NormalBalance.Credit,
            AccountNature.Equity => NormalBalance.Credit,
            AccountNature.Revenue => NormalBalance.Credit,
            _ => NormalBalance.Debit
        };
}

public enum AccountLevel
{
    Group = 1,       // گروه حساب (۱ رقمی، مانند ۱: دارایی‌ها)
    General = 2,     // حساب کل (۲ تا ۴ رقمی، مانند ۱۰: دارایی‌های جاری)
    Subsidiary = 3,  // حساب معین (۴ تا ۶ رقمی، مانند ۱۰۱۰۱: موجودی نقد نزد بانک‌ها)
    Detail = 4       // حساب تفصیلی (شناور / چندسطحی)
}

public enum FiscalCalendarType
{
    Persian = 1,     // تقویم هجری شمسی (جلالی)
    Gregorian = 2    // تقویم میلادی استاندارد
}

public enum FiscalYearStatus
{
    Draft = 1,       // پیش‌نویس (در حال تعریف و تنظیم دوره‌ها)
    Open = 2,        // باز (آماده دریافت اسناد و عملیات روزانه)
    Closing = 3,     // در حال اختتامیه و تعدیلات پایان سال
    Closed = 4       // بسته قطعی (دفاتر قطعی شده و انتقال مانده انجام یافته)
}

public enum FiscalPeriodStatus
{
    Draft = 1,       // پیش‌نویس
    Open = 2,        // باز (پذیرش اسناد جدید)
    Locked = 3,      // قفل موقت (جهت بررسی و گزارش‌گیری میان‌دوره‌ای)
    Closed = 4       // بسته قطعی (غیرقابل بازگشایی عادی و عدم پذیرش سند)
}

public enum PeriodGenerationType
{
    Monthly = 1,     // ۱۲ دوره ماهانه
    Quarterly = 2,   // ۴ دوره فصلی
    Custom = 3       // دوره‌های سفارشی دستی
}

public enum RoundingMode
{
    RoundToEven = 1,   // گرد کردن به نزدیک‌ترین زوج (استاندارد بانکی Banker's Rounding)
    AwayFromZero = 2,  // گرد کردن به دور از صفر (ریاضی کلاسیک 0.5 به بالا)
    Floor = 3,         // جزء صحیح رو به پایین
    Ceiling = 4        // جزء صحیح رو به بالا
}

public enum VoucherNumberingPolicy
{
    ContinuousByFiscalYear = 1, // شماره‌گذاری متوالی در کل سال مالی (پیش‌فرض)
    MonthlyRestart = 2,         // شروع مجدد شماره‌گذاری از ۱ در هر ماه/دوره مالی
    DailyRestart = 3            // شروع مجدد شماره‌گذاری از ۱ در هر روز
}

public enum AllowNegativeBalancesPolicy
{
    StrictlyForbidden = 1,      // ممنوعیت قطعی مانده منفی غیرعادی
    WarningOnly = 2,            // هشدار در زمان ثبت سند
    Allowed = 3                 // مجاز بدون محدودیت
}

public enum DocumentApprovalPolicy
{
    DirectPostingAllowed = 1,      // ثبت مستقیم و بدون نیاز به تاییدیه چندمرحله‌ای
    TwoStepApprovalRequired = 2,   // پیش‌نویس -> تصویب و ثبت قطعی
    ThreeStepApprovalRequired = 3  // پیش‌نویس -> بررسی کارشناس -> تایید نهایی مدیر مالی
}

/// <summary>
/// منشأ (Source) اسناد حسابداری. اسناد سیستمی باید همیشه منشأ مشخص و قابل ردیابی داشته باشند.
/// قرارداد ارتباطی ماژول‌های دیگر با موتور سند از طریق این مقادیر برقرار می‌شود.
/// </summary>
public static class DocumentSourceModules
{
    public const string Manual = "Manual";             // سند دستی کاربر
    public const string Accounting = "Accounting";     // اسناد داخلی حسابداری (معکوس / تعدیل)
    public const string Sales = "Sales";               // فروش و بازرگانی
    public const string Purchasing = "Purchasing";     // خرید و تدارکات
    public const string Inventory = "Inventory";       // انبار و گردش موجودی
    public const string Treasury = "Treasury";         // خزانه‌داری و نقدینگی
    public const string Payroll = "Payroll";           // حقوق و دستمزد

    public static readonly IReadOnlyList<string> All =
    [
        Manual, Accounting, Sales, Purchasing, Inventory, Treasury, Payroll
    ];

    public static bool IsKnown(string? sourceModule) =>
        !string.IsNullOrWhiteSpace(sourceModule) && All.Contains(sourceModule.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string sourceModule) =>
        All.First(s => s.Equals(sourceModule.Trim(), StringComparison.OrdinalIgnoreCase));
}
