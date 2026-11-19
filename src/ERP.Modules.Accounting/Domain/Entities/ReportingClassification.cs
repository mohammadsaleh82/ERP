using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// تنظیمات گزارش‌گیری: طبقه‌بندی ریشه‌های کدینگ حساب برای صورت‌های مالی.
/// هر ریشه کد دو رقمی (مانند «۱۰»، «۲۱») به یک طبقه‌بندی گزارش نگاشت می‌شود؛
/// حساب‌های بدون تنظیم با پیش‌فرض ماهیت (Asset→جاری، Liability→جاری و ...)
/// طبقه‌بندی می‌شوند.
/// </summary>
public class ReportingClassification : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>کد ریشه دو رقمی کدینگ حساب (FullCode حساب با این پیشوند آغاز می‌شود).</summary>
    public string RootCode { get; set; } = string.Empty;

    public ReportAccountCategory Category { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    private ReportingClassification()
    {
    }

    public static ReportingClassification Create(
        Guid tenantId, Guid companyId, string rootCode, ReportAccountCategory category)
    {
        var normalized = NormalizeRootCode(rootCode);
        if (normalized is null)
            throw new AccountingDomainException("Accounting.InvalidClassificationRootCode",
                "کد ریشه طبقه‌بندی گزارش باید دقیقاً دو رقم باشد.");

        return new ReportingClassification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            RootCode = normalized,
            Category = category,
            Version = 1
        };
    }

    public static string? NormalizeRootCode(string? rootCode)
    {
        var digits = new string((rootCode ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 2 ? digits : null;
    }
}

/// <summary>
/// طبقه‌بندی جریان نقد یک حساب برای روش مستقیم جریان وجوه نقد — به‌کارگیری
/// جابه‌جایی‌های حساب وجوه نقد به سبد عملیاتی/سرمایه‌گذاری/تأمین‌مالی.
/// حساب‌های بدون تنظیم، بر اساس طبقه‌بندی گزارش حساب مقابل (دارایی غیرجاری →
/// سرمایه‌گذاری، حقوق مالکانه/بدهی غیرجاری → تأمین مالی، سایر → عملیاتی) سبد می‌گیرند.
/// </summary>
public class CashFlowClassification : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public CashFlowCategory Category { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    private CashFlowClassification()
    {
    }

    public static CashFlowClassification Create(
        Guid tenantId, Guid companyId, Guid accountId, CashFlowCategory category)
    {
        if (accountId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyAccountId", "حساب طبقه‌بندی جریان نقد الزامی است.");

        return new CashFlowClassification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            AccountId = accountId,
            Category = category,
            Version = 1
        };
    }
}
