using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Contracts.Interfaces;

/// <summary>
/// قرارداد ارتباطی ماژول حسابداری با سایر ماژول‌ها (Sales / Purchasing / Inventory / Treasury / Payroll).
/// هر ماژول خارجی برای ایجاد سند سیستمی باید منشأ مشخص و قابل ردیابی ارسال کند:
/// SourceModule (نام ماژول مبدأ) + SourceDocumentId (شناسه رکورد مبدأ) + ReferenceNumber (اختیاری).
/// هیچ مانده مالی بدون منبع قابل ردیابی ایجاد نمی‌شود.
/// </summary>
public interface IAccountingModuleApi
{
    /// <summary>
    /// ایجاد سند سیستمی از یک منشأ مشخص و (به‌صورت پیش‌فرض) قطعی‌سازی اتمیک آن.
    /// عملیات Idempotent است: کلید یکسان سند موجود را بازمی‌گرداند.
    /// </summary>
    Task<VoucherFromSourceResult> CreateVoucherFromSourceAsync(
        CreateVoucherFromSourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>جستجوی اسناد یک منشأ سیستمی (برای ردیابی معکوس: از سند عملیاتی به سند حسابداری).</summary>
    Task<IReadOnlyList<VoucherFromSourceSummary>> GetVouchersBySourceAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);
}

public sealed record VoucherLineFromSource(
    Guid AccountId,
    DebitCredit Side,
    decimal Amount,
    string Description,
    Guid? CostCenterId = null,
    Guid? ProjectId = null,
    Guid? DetailAccountId = null,
    string? ForeignCurrencyCode = null,
    decimal? ForeignAmount = null,
    decimal ExchangeRate = 1.0m);

public sealed record CreateVoucherFromSourceRequest(
    Guid TenantId,
    Guid CompanyId,
    DateOnly DocumentDate,
    string Description,
    string SourceModule,
    Guid SourceDocumentId,
    IReadOnlyList<VoucherLineFromSource> Lines,
    Guid? FiscalYearId = null,
    string? ReferenceNumber = null,
    string? IdempotencyKey = null,
    bool AutoPost = true,
    string PerformedBy = "system");

public sealed record VoucherFromSourceResult(
    Guid DocumentId,
    long DocumentNumber,
    DocumentStatus Status,
    decimal TotalDebit,
    decimal TotalCredit,
    string SourceModule,
    Guid SourceDocumentId,
    bool WasAlreadyExisting,
    bool WasAlreadyPosted);

public sealed record VoucherFromSourceSummary(
    Guid DocumentId,
    long DocumentNumber,
    DateOnly DocumentDate,
    DocumentStatus Status,
    decimal TotalDebit,
    decimal TotalCredit,
    string? ReferenceNumber);
