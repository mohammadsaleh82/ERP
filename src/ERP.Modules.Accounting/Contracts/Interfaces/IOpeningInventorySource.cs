using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Contracts.Interfaces;

/// <summary>
/// قرارداد دریافت مانده‌های افتتاحیه انبار از ماژول Inventory.
/// ماژول Inventory ارزیابی مانده قطعی پایان سال خود را از طریق این قرارداد به
/// موتور افتتاحیه می‌دهد؛ هیچ رفرنس مستقیمی بین Entityهای دو ماژول وجود ندارد.
/// اتصال واقعی در Composition Root (ERP.Api) با آداپتور ماژول Inventory انجام می‌شود.
/// مبالغ به ارز پایه و «علامت‌دار» هستند: بدهکار مثبت، بستانکار منفی.
/// </summary>
public interface IOpeningInventorySource
{
    /// <summary>
    /// خطوط افتتاحیه انبار برای سال مالی جدید (از ارزیابی تأییدشده پایان سال قبل).
    /// هر خط: حساب انبار + مبلغ علامت‌دار + اختیاری بعد تفصیلی (کالا/گروه کالا).
    /// </summary>
    Task<IReadOnlyList<OpeningInventoryLine>> GetOpeningLinesAsync(
        Guid tenantId,
        Guid companyId,
        Guid targetFiscalYearId,
        CancellationToken cancellationToken = default);
}

public sealed record OpeningInventoryLine(
    Guid AccountId,
    decimal SignedAmount,
    string Description,
    Guid? DetailDimensionId = null,
    FinancialDimensionType? DetailDimensionType = null);

/// <summary>پیاده‌سازی پیش‌فرض: تا زمان آماده شدن ماژول Inventory هیچ خطی برنمی‌گرداند.</summary>
public class EmptyOpeningInventorySource : IOpeningInventorySource
{
    public Task<IReadOnlyList<OpeningInventoryLine>> GetOpeningLinesAsync(
        Guid tenantId, Guid companyId, Guid targetFiscalYearId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OpeningInventoryLine>>([]);
}
