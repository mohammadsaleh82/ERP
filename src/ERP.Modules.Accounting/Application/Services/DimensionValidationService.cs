using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IDimensionValidationService
{
    /// <summary>
    /// اعتبارسنجی کامل ابعاد مالی ردیف‌های سند: وجود، نوع، شرکت مالک، فعال بودن،
    /// سیاست‌های حساب (اجباری/مجاز/ممنوع) و ترکیب‌های ممنوع.
    /// هم در ایجاد/ویرایش پیش‌نویس و هم در قطعی‌سازی سند (دفاع در عمق) اجرا می‌شود.
    /// </summary>
    Task ValidateDocumentLinesAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<DocumentLineValidationInput> lines,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentLineValidationInput(
    Guid AccountId,
    int LineNumber,
    IReadOnlyList<(Domain.Enums.FinancialDimensionType Type, Guid DimensionId)> Assignments);

/// <summary>
/// پیاده‌سازی Application اعتبارسنجی ابعاد: بارگذاری یک‌باره داده‌های لازم
/// (ابعاد، سیاست‌های حساب، قواعد ترکیب) و اجرای قواعد خالص دامنه
/// (<see cref="Domain.Services.DimensionValidationService"/>) بدون N+1.
/// </summary>
public class DimensionValidationApplicationService : IDimensionValidationService
{
    private readonly AccountingDbContext _db;

    public DimensionValidationApplicationService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task ValidateDocumentLinesAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<DocumentLineValidationInput> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
            return;

        var dimensionIds = lines
            .SelectMany(l => l.Assignments)
            .Select(a => a.DimensionId)
            .Distinct()
            .ToList();

        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();

        var dimensionLookup = dimensionIds.Count == 0
            ? new Dictionary<Guid, Domain.Services.DimensionValidationService.DimensionMeta>()
            : (await _db.FinancialDimensions
                .AsNoTracking()
                .IgnoreQueryFilters()
                // فیلتر صریح فقط Tenant: بعد متعلق به شرکت دیگر باید در لایه قواعد با
                // خطای اختصاصی DimensionForeignCompany رد شود، نه «یافت نشد»
                // (فیلتر سراسری Company اینجا باید دور زده شود تا خطای درست صادر شود)
                .Where(d => d.TenantId == tenantId && dimensionIds.Contains(d.Id))
                .Select(d => new Domain.Services.DimensionValidationService.DimensionMeta(
                    d.Id, d.DimensionType, d.CompanyId, d.IsActive, d.Code))
                .ToListAsync(cancellationToken))
                .ToDictionary(d => d.Id);

        // حساب‌ها با فیلتر صریح Tenant بارگذاری می‌شوند؛ حسابِ متعلق به شرکت دیگر از
        // اعتبارسنجی ابعاد کنار گذاشته می‌شود چون اعتبارسنج حساب‌ها در مسیر قطعی‌سازی
        // آن را با خطای رسمی «حساب شرکت دیگر» رد می‌کند (خطای کانonical این سناریو).
        var accountRows = await _db.Accounts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.CompanyId,
                a.FullCode,
                a.RequireCostCenter,
                a.RequireProject,
                a.RequireDetailAccount,
                Policies = a.DimensionPolicies
                    .Select(p => new { p.DimensionType, p.Mode })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var foreignAccountIds = accountRows
            .Where(a => a.CompanyId != companyId)
            .Select(a => a.Id)
            .ToHashSet();

        var accountLookup = accountRows
            .Where(a => a.CompanyId == companyId)
            .ToDictionary(
                a => a.Id,
                a => new Domain.Services.DimensionValidationService.AccountDimensionMeta(
                    a.Id,
                    a.FullCode,
                    a.RequireCostCenter,
                    a.RequireProject,
                    a.RequireDetailAccount,
                    a.Policies.Select(p => (p.DimensionType, p.Mode)).ToList()));

        var combinationRules = await _db.DimensionCombinationRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId)
            .Select(r => new Domain.Services.DimensionValidationService.CombinationRuleInput(
                r.FirstDimensionType, r.FirstDimensionId, r.SecondDimensionType, r.SecondDimensionId))
            .ToListAsync(cancellationToken);

        Domain.Services.DimensionValidationService.ValidateLines(
            lines
                .Where(l => !foreignAccountIds.Contains(l.AccountId))
                .Select(l => new Domain.Services.DimensionValidationService.LineDimensionInput(
                    l.LineNumber,
                    l.AccountId,
                    l.Assignments)).ToList(),
            accountLookup,
            dimensionLookup,
            companyId,
            combinationRules);
    }
}
