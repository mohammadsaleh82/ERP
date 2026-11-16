using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Contracts.Interfaces;

/// <summary>
/// قرارداد ارتباطی ابعاد مالی ماژول حسابداری با سایر ماژول‌ها.
/// ماژول‌های مالک مفاهیم شناور (Parties برای شخص/مشتری/تأمین‌کننده، Payroll برای
/// کارمند، Sales/Purchasing/Inventory برای قراردادها و ...) بدون هیچ رفرنس مستقیم به
/// Entityهای داخلی حسابداری — و بالعکس — از طریق این قرارداد ابعاد خود را در
/// رجیستری ابعاد مالی «به‌صورت آینه مرجع» ثبت و همگام می‌کنند.
/// الگوی مصرف در Composition Root: ماژول مالک یک آداپتور تعریف می‌کند و ERP.Api
/// آن را به این قرارداد متصل می‌کند؛ هیچ وابستگی پروژه‌ای بین ماژول‌ها ایجاد نمی‌شود.
/// </summary>
public interface IFinancialDimensionApi
{
    /// <summary>
    /// ثبت یا همگام‌سازی یک بعد مالی از ماژول مالک (Upsert بر اساس مرجع خارجی).
    /// Idempotent: ثبت با همان (شرکت، نوع، مرجع خارجی) ردیف موجود را به‌روز می‌کند.
    /// </summary>
    Task<FinancialDimensionContractDto> RegisterAsync(
        RegisterFinancialDimensionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>همگام‌سازی کد/نام/وضعیت فعال یک بعد ثبت‌شده از ماژول مالک.</summary>
    Task<FinancialDimensionContractDto> SyncAsync(
        SyncFinancialDimensionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>وجود و فعال بودن یک بعد برای ماژول مالک.</summary>
    Task<bool> IsActiveAsync(
        Guid tenantId,
        Guid companyId,
        FinancialDimensionType dimensionType,
        Guid externalReferenceId,
        CancellationToken cancellationToken = default);

    /// <summary>دریافت بعد ثبت‌شده بر اساس مرجع خارجی ماژول مالک.</summary>
    Task<FinancialDimensionContractDto?> GetByExternalReferenceAsync(
        Guid tenantId,
        Guid companyId,
        FinancialDimensionType dimensionType,
        Guid externalReferenceId,
        CancellationToken cancellationToken = default);
}

public sealed record RegisterFinancialDimensionRequest(
    Guid TenantId,
    Guid CompanyId,
    FinancialDimensionType DimensionType,
    Guid ExternalReferenceId,
    string Code,
    string Name,
    string OwnerModule,
    string PerformedBy);

public sealed record SyncFinancialDimensionRequest(
    Guid TenantId,
    Guid CompanyId,
    FinancialDimensionType DimensionType,
    Guid ExternalReferenceId,
    string? Code,
    string? Name,
    bool? IsActive,
    string PerformedBy);

public sealed record FinancialDimensionContractDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    FinancialDimensionType DimensionType,
    string Code,
    string Name,
    Guid? ExternalReferenceId,
    string? OwnerModule,
    bool IsActive);
