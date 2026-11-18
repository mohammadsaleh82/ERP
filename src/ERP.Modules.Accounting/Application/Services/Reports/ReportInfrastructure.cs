using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services.Reports;

// ===========================================================================
// مرزهای زمانی گزارش
// ===========================================================================

public static class ReportRangeResolver
{
    /// <summary>
    /// مرزهای زمانی گزارش از سال/دوره مالی و بازه تاریخ: مرزهای داده‌شده داخل
    /// محدوده سال/دوره محصور می‌شوند؛ مرز «قبل از گردش» شروع بازه (یا شروع سال) است.
    /// </summary>
    public static async Task<ReportRange> ResolveAsync(AccountingDbContext db, ReportFilter filter, CancellationToken ct)
    {
        var from = filter.FromDate;
        var to = filter.ToDate;

        if (filter.FiscalPeriodId.HasValue)
        {
            var period = await db.FiscalPeriods.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == filter.TenantId && p.CompanyId == filter.CompanyId && p.Id == filter.FiscalPeriodId, ct)
                ?? throw new NotFoundException("FiscalPeriod", filter.FiscalPeriodId);
            from = from ?? period.StartDate;
            to = to ?? period.EndDate;
            from = Max(from, period.StartDate);
            to = Min(to, period.EndDate);
        }
        else if (filter.FiscalYearId.HasValue)
        {
            var year = await db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.TenantId == filter.TenantId && y.CompanyId == filter.CompanyId && y.Id == filter.FiscalYearId, ct)
                ?? throw new NotFoundException("FiscalYear", filter.FiscalYearId);
            from = from ?? year.StartDate;
            to = to ?? year.EndDate;
            from = Max(from, year.StartDate);
            to = Min(to, year.EndDate);
        }

        if (from is null && to is null)
        {
            // بدون مرز مالی/تاریخی: کل دفتر تا امروز
            to = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var effectiveFrom = from ?? to!.Value;
        var effectiveTo = to ?? from!.Value;
        var openingBoundary = from; // مانده پیش از شروع بازه؛ null یعنی ابتدای دفتر
        return new ReportRange(effectiveFrom, effectiveTo, openingBoundary);
    }

    private static DateOnly Max(DateOnly? a, DateOnly b) => a.HasValue && a.Value > b ? a.Value : b;
    private static DateOnly Min(DateOnly? a, DateOnly b) => a.HasValue && a.Value < b ? a.Value : b;
}

// ===========================================================================
// سازنده پرس‌وجوی مشترک دفتر کل با فیلترهای ابعادی
// ===========================================================================

public static class ReportQueryBuilder
{
    /// <summary>
    /// پرس‌وجوی پایه سطرهای دفتر کل داخل بازه گزارش با اعمال فیلترهای شرکت/شعبه/
    /// حساب/ارز/شخص/مرکز هزینه/پروژه. اسناد Draft هرگز در دفتر کل وجود ندارند.
    /// </summary>
    public static IQueryable<Domain.Entities.GeneralLedgerEntry> Build(
        AccountingDbContext db,
        ReportFilter filter,
        ReportRange range,
        bool openingSegment)
    {
        var query = db.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TenantId == filter.TenantId && e.CompanyId == filter.CompanyId);

        query = openingSegment
            ? query.Where(e => range.OpeningBoundary == null || e.EntryDate < range.OpeningBoundary.Value)
            : query.Where(e => e.EntryDate >= range.FromDate && e.EntryDate <= range.ToDate);

        if (filter.FiscalYearId.HasValue)
            query = query.Where(e => e.FiscalYearId == filter.FiscalYearId.Value);

        if (filter.BranchId.HasValue)
            query = query.Where(e => e.BranchId == filter.BranchId.Value);

        if (!string.IsNullOrWhiteSpace(filter.CurrencyCode))
            query = query.Where(e => e.CurrencyCode == filter.CurrencyCode);

        // فیلترهای ابعادی: هر نوع بعد یک شرط Contains مستقل (AND) می‌سازد
        if (filter.PartyId.HasValue)
        {
            var partyTypes = new[]
            {
                filter.PartyDimensionType ?? FinancialDimensionType.Party,
                FinancialDimensionType.Customer,
                FinancialDimensionType.Supplier
            }.Distinct().ToList();

            query = query.Where(e => db.GeneralLedgerEntryDimensions
                .Any(d => d.GeneralLedgerEntryId == e.Id
                          && d.TenantId == filter.TenantId
                          && d.CompanyId == filter.CompanyId
                          && partyTypes.Contains(d.DimensionType)
                          && d.DimensionId == filter.PartyId.Value));
        }

        if (filter.CostCenterId.HasValue)
        {
            var ccId = filter.CostCenterId.Value;
            query = query.Where(e => db.GeneralLedgerEntryDimensions
                .Any(d => d.GeneralLedgerEntryId == e.Id
                          && d.TenantId == filter.TenantId
                          && d.CompanyId == filter.CompanyId
                          && d.DimensionType == FinancialDimensionType.CostCenter
                          && d.DimensionId == ccId));
        }

        if (filter.ProjectId.HasValue)
        {
            var projectId = filter.ProjectId.Value;
            query = query.Where(e => db.GeneralLedgerEntryDimensions
                .Any(d => d.GeneralLedgerEntryId == e.Id
                          && d.TenantId == filter.TenantId
                          && d.CompanyId == filter.CompanyId
                          && d.DimensionType == FinancialDimensionType.Project
                          && d.DimensionId == projectId));
        }

        return query;
    }

    /// <summary>شناسه حساب‌های مجاز گزارش بر اساس فیلتر حساب/سطح (Projection سبک).</summary>
    public static async Task<List<Guid>> ResolveAccountIdsAsync(
        AccountingDbContext db, ReportFilter filter, CancellationToken ct)
    {
        var query = db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId && a.AllowPosting);

        if (filter.AccountId.HasValue)
            query = query.Where(a => a.Id == filter.AccountId.Value);

        if (!string.IsNullOrWhiteSpace(filter.AccountCodePrefix))
            query = query.Where(a => a.FullCode.StartsWith(filter.AccountCodePrefix));

        if (filter.AccountLevel.HasValue)
            query = query.Where(a => a.Level == filter.AccountLevel.Value);

        return await query.Select(a => a.Id).ToListAsync(ct);
    }
}

// ===========================================================================
// طبقه‌بندی گزارش‌گیری (از تنظیمات شرکت با پیش‌فرض ماهیت)
// ===========================================================================

public interface IReportClassificationService
{
    /// <summary>طبقه‌بندی هر حساب: تنظیمات ریشه کدینگ، در نبود تنظیم پیش‌فرض ماهیت.</summary>
    Task<Dictionary<Guid, ReportAccountCategory>> ResolveAccountCategoriesAsync(
        Guid tenantId, Guid companyId, CancellationToken ct = default);

    /// <summary>حساب‌های یک طبقه‌بندی (برای وجوه نقد/دریافتنی/پرداختنی).</summary>
    Task<List<Guid>> GetAccountsOfCategoryAsync(
        Guid tenantId, Guid companyId, ReportAccountCategory category, CancellationToken ct = default);

    Task<IReadOnlyList<ReportingClassificationDto>> GetSettingsAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    /// <summary>ثبت/به‌روزرسانی Idempotent تنظیم ریشه.</summary>
    Task<ReportingClassificationDto> UpsertAsync(Guid tenantId, Guid companyId, string rootCode, ReportAccountCategory category, CancellationToken ct = default);

    Task<IReadOnlyList<CashFlowClassificationDto>> GetCashFlowSettingsAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    Task<CashFlowClassificationDto> UpsertCashFlowAsync(Guid tenantId, Guid companyId, Guid accountId, CashFlowCategory category, CancellationToken ct = default);

    /// <summary>سبد جریان نقد حساب مقابل: تنظیم صریح یا پیش‌فرض طبقه‌بندی گزارش.</summary>
    CashFlowCategory ResolveCashFlowBucket(Guid accountId, ReportAccountCategory accountCategory, Dictionary<Guid, CashFlowCategory> overrides);
}

public class ReportClassificationService : IReportClassificationService
{
    private readonly AccountingDbContext _db;

    public ReportClassificationService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<Guid, ReportAccountCategory>> ResolveAccountCategoriesAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var settings = (await GetSettingsAsync(tenantId, companyId, ct)).ToDictionary(s => s.RootCode, s => s.Category);
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new { a.Id, a.FullCode, a.Nature })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, ReportAccountCategory>(accounts.Count);
        foreach (var account in accounts)
        {
            var root = account.FullCode.Length >= 2 ? account.FullCode[..2] : account.FullCode;
            result[account.Id] = settings.TryGetValue(root, out var category)
                ? category
                : DefaultCategory(account.Nature);
        }

        return result;
    }

    public async Task<List<Guid>> GetAccountsOfCategoryAsync(Guid tenantId, Guid companyId, ReportAccountCategory category, CancellationToken ct = default)
    {
        var resolved = await ResolveAccountCategoriesAsync(tenantId, companyId, ct);
        return resolved.Where(kv => kv.Value == category).Select(kv => kv.Key).ToList();
    }

    public async Task<IReadOnlyList<ReportingClassificationDto>> GetSettingsAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var rows = await _db.ReportingClassifications.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.CompanyId == companyId)
            .OrderBy(c => c.RootCode)
            .Select(c => new ReportingClassificationDto(c.Id, c.RootCode, c.Category))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<ReportingClassificationDto> UpsertAsync(Guid tenantId, Guid companyId, string rootCode, ReportAccountCategory category, CancellationToken ct = default)
    {
        var normalized = ReportingClassification.NormalizeRootCode(rootCode)
            ?? throw new AccountingDomainException("Accounting.InvalidClassificationRootCode",
                "کد ریشه طبقه‌بندی گزارش باید دقیقاً دو رقم باشد.");

        var existing = await _db.ReportingClassifications
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CompanyId == companyId && c.RootCode == normalized, ct);

        if (existing is null)
        {
            var created = ReportingClassification.Create(tenantId, companyId, normalized, category);
            _db.ReportingClassifications.Add(created);
            await _db.SaveChangesAsync(ct);
            return new ReportingClassificationDto(created.Id, created.RootCode, created.Category);
        }

        existing.Category = category;
        await _db.SaveChangesAsync(ct);
        return new ReportingClassificationDto(existing.Id, existing.RootCode, existing.Category);
    }

    public async Task<IReadOnlyList<CashFlowClassificationDto>> GetCashFlowSettingsAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var rows = await _db.CashFlowClassifications.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.CompanyId == companyId)
            .Select(c => new CashFlowClassificationDto(c.Id, c.AccountId, c.Category))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<CashFlowClassificationDto> UpsertCashFlowAsync(Guid tenantId, Guid companyId, Guid accountId, CashFlowCategory category, CancellationToken ct = default)
    {
        var existing = await _db.CashFlowClassifications
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CompanyId == companyId && c.AccountId == accountId, ct);

        if (existing is null)
        {
            var created = CashFlowClassification.Create(tenantId, companyId, accountId, category);
            _db.CashFlowClassifications.Add(created);
            await _db.SaveChangesAsync(ct);
            return new CashFlowClassificationDto(created.Id, created.AccountId, created.Category);
        }

        existing.Category = category;
        await _db.SaveChangesAsync(ct);
        return new CashFlowClassificationDto(existing.Id, existing.AccountId, existing.Category);
    }

    public CashFlowCategory ResolveCashFlowBucket(Guid accountId, ReportAccountCategory accountCategory, Dictionary<Guid, CashFlowCategory> overrides)
        => overrides.TryGetValue(accountId, out var bucket)
            ? bucket
            : accountCategory switch
            {
                ReportAccountCategory.NonCurrentAsset => CashFlowCategory.Investing,
                ReportAccountCategory.Equity => CashFlowCategory.Financing,
                ReportAccountCategory.NonCurrentLiability => CashFlowCategory.Financing,
                _ => CashFlowCategory.Operating
            };

    private static ReportAccountCategory DefaultCategory(AccountNature nature) => nature switch
    {
        AccountNature.Asset => ReportAccountCategory.CurrentAsset,
        AccountNature.Liability => ReportAccountCategory.CurrentLiability,
        AccountNature.Equity => ReportAccountCategory.Equity,
        AccountNature.Revenue => ReportAccountCategory.Revenue,
        AccountNature.Expense => ReportAccountCategory.OperatingExpense,
        _ => ReportAccountCategory.CurrentAsset
    };
}
