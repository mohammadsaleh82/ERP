using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// اسنپ‌شات تجمیعی مانده حساب (Materialized Read Model).
/// یک ردیف به ازای هر (شرکت، دوره مالی، حساب) با گردش بدهکار و بستانکار همان دوره.
/// به‌صورت اتمیک با نوشتن سطرهای دفتر کل در همان تراکنش Posting به‌روز می‌شود و
/// در صورت مغایرت از طریق Rebuild کامل از دفتر کل بازسازی می‌گردد.
/// این جدول هرگز جایگزین منبع اصلی گردش (acc_gl_entries) نیست و فقط مسیرِ خواندنِ
/// سریع گزارش‌هاست؛ اعتبار آن همیشه با Reconciliation قابل اثبات است.
/// </summary>
public class AccountBalanceSnapshot : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid AccountId { get; set; }

    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public AccountBalanceSnapshot()
    {
    }

    public AccountBalanceSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid fiscalPeriodId,
        Guid accountId,
        decimal periodDebit = 0m,
        decimal periodCredit = 0m)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        FiscalPeriodId = fiscalPeriodId;
        AccountId = accountId;
        PeriodDebit = periodDebit;
        PeriodCredit = periodCredit;
        Version = 1;
    }

    /// <summary>افزودن گردش جدید به اسنپ‌شات موجود (در همان تراکنش Posting).</summary>
    public void Accumulate(decimal debit, decimal credit)
    {
        PeriodDebit += debit;
        PeriodCredit += credit;
        Version++;
    }
}
