using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services.Reports;

// ===========================================================================
// ترازنامه
// ===========================================================================

public interface IFinancialStatementsService
{
    /// <summary>
    /// ترازنامه بر اساس طبقه‌بندی گزارش‌گیری تا تاریخ مشخص — با ردیف «نتیجه دوره
    /// جاری» در حقوق مالکانه تا اجزای صورت در هر مقطعی (حتی پیش از اختتامیه) متوازن باشند.
    /// </summary>
    Task<BalanceSheetDto> GetBalanceSheetAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>سود و زیان بازه با طبقه‌بندی از تنظیمات گزارش‌گیری.</summary>
    Task<ProfitLossDto> GetProfitLossAsync(ReportFilter filter, CancellationToken ct = default);
}

public class FinancialStatementsService : IFinancialStatementsService
{
    private readonly AccountingDbContext _db;
    private readonly IReportClassificationService _classification;

    public FinancialStatementsService(AccountingDbContext db, IReportClassificationService classification)
    {
        _db = db;
        _classification = classification;
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        // مانده تا پایان بازه (بازهای بازه برای ترازنامه فقط مرز «تا» اهمیت دارد)
        var balances = await ReportQueryBuilder.Build(_db, filter, range with { OpeningBoundary = null }, openingSegment: true)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        var categories = await _classification.ResolveAccountCategoriesAsync(filter.TenantId, filter.CompanyId, ct);

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId)
            .Select(a => new { a.Id, a.FullCode, a.Name })
            .ToListAsync(ct);
        var accountById = accounts.ToDictionary(a => a.Id);

        var currentAssets = new List<StatementRowDto>();
        var nonCurrentAssets = new List<StatementRowDto>();
        var currentLiabilities = new List<StatementRowDto>();
        var nonCurrentLiabilities = new List<StatementRowDto>();
        var equity = new List<StatementRowDto>();

        decimal revenueNet = 0m, expenseNet = 0m;

        foreach (var balance in balances)
        {
            var signed = balance.Debit - balance.Credit;
            if (signed == 0m || !accountById.TryGetValue(balance.AccountId, out var account))
                continue;

            categories.TryGetValue(balance.AccountId, out var category);
            var row = new StatementRowDto(account.Id, account.FullCode, account.Name, Math.Abs(signed), category);

            switch (category)
            {
                case ReportAccountCategory.CurrentAsset or ReportAccountCategory.AccountsReceivable
                    or ReportAccountCategory.CashAndCashEquivalents:
                    currentAssets.Add(row);
                    break;
                case ReportAccountCategory.NonCurrentAsset:
                    nonCurrentAssets.Add(row);
                    break;
                case ReportAccountCategory.CurrentLiability or ReportAccountCategory.AccountsPayable:
                    // بدهی: مانده بستانکار طبیعی است؛ مانده بدهکار (بستانکار حساب) قراردادی
                    currentLiabilities.Add(row);
                    break;
                case ReportAccountCategory.NonCurrentLiability:
                    nonCurrentLiabilities.Add(row);
                    break;
                case ReportAccountCategory.Equity:
                    equity.Add(row);
                    break;
                case ReportAccountCategory.Revenue or ReportAccountCategory.OtherIncome:
                    revenueNet += -signed;
                    break;
                case ReportAccountCategory.CostOfGoodsSold or ReportAccountCategory.OperatingExpense
                    or ReportAccountCategory.OtherExpense or ReportAccountCategory.TaxExpense:
                    expenseNet += signed;
                    break;
            }
        }

        // نتیجه دوره جاری: درآمد منهای هزینه تا تاریخ گزارش — دارایی = بدهی + حقوق مالکانه
        var currentPeriodResult = revenueNet - expenseNet;
        if (currentPeriodResult != 0m)
        {
            equity.Add(new StatementRowDto(
                Guid.Empty, "-", "نتیجه دوره جاری (سود و زیان)",
                Math.Abs(currentPeriodResult), ReportAccountCategory.Equity));
        }

        decimal Sum(List<StatementRowDto> rows) => rows.Sum(r => r.Balance);

        var currentAssetsTotal = Sum(currentAssets);
        var nonCurrentAssetsTotal = Sum(nonCurrentAssets);
        var totalAssets = currentAssetsTotal + nonCurrentAssetsTotal;
        var currentLiabilitiesTotal = Sum(currentLiabilities);
        var nonCurrentLiabilitiesTotal = Sum(nonCurrentLiabilities);
        var totalLiabilities = currentLiabilitiesTotal + nonCurrentLiabilitiesTotal;
        var totalEquity = Sum(equity);

        SortByCode(currentAssets);
        SortByCode(nonCurrentAssets);
        SortByCode(currentLiabilities);
        SortByCode(nonCurrentLiabilities);
        SortByCode(equity);

        return new BalanceSheetDto(
            filter.CompanyId, range.ToDate,
            new StatementSectionDto("دارایی جاری", currentAssets, currentAssetsTotal),
            new StatementSectionDto("دارایی غیرجاری", nonCurrentAssets, nonCurrentAssetsTotal),
            totalAssets,
            new StatementSectionDto("بدهی جاری", currentLiabilities, currentLiabilitiesTotal),
            new StatementSectionDto("بدهی غیرجاری", nonCurrentLiabilities, nonCurrentLiabilitiesTotal),
            totalLiabilities,
            new StatementSectionDto("حقوق مالکانه", equity, totalEquity),
            currentPeriodResult,
            totalEquity,
            totalAssets == totalLiabilities + totalEquity,
            totalAssets - (totalLiabilities + totalEquity));
    }

    public async Task<ProfitLossDto> GetProfitLossAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        var turnover = await ReportQueryBuilder.Build(_db, filter, range, openingSegment: false)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        var categories = await _classification.ResolveAccountCategoriesAsync(filter.TenantId, filter.CompanyId, ct);

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId)
            .Select(a => new { a.Id, a.FullCode, a.Name })
            .ToListAsync(ct);
        var accountById = accounts.ToDictionary(a => a.Id);

        var revenues = new List<StatementRowDto>();
        var cogs = new List<StatementRowDto>();
        var operatingExpenses = new List<StatementRowDto>();
        var otherIncome = new List<StatementRowDto>();
        var otherExpenses = new List<StatementRowDto>();
        var taxExpenses = new List<StatementRowDto>();

        foreach (var balance in turnover)
        {
            var signed = balance.Debit - balance.Credit;
            if (signed == 0m || !accountById.TryGetValue(balance.AccountId, out var account))
                continue;

            categories.TryGetValue(balance.AccountId, out var category);
            var row = new StatementRowDto(account.Id, account.FullCode, account.Name, Math.Abs(signed), category);

            switch (category)
            {
                case ReportAccountCategory.Revenue:
                    revenues.Add(row);
                    break;
                case ReportAccountCategory.CostOfGoodsSold:
                    cogs.Add(row);
                    break;
                case ReportAccountCategory.OperatingExpense:
                    operatingExpenses.Add(row);
                    break;
                case ReportAccountCategory.OtherIncome:
                    otherIncome.Add(row);
                    break;
                case ReportAccountCategory.OtherExpense:
                    otherExpenses.Add(row);
                    break;
                case ReportAccountCategory.TaxExpense:
                    taxExpenses.Add(row);
                    break;
            }
        }

        decimal Sum(List<StatementRowDto> rows) => rows.Sum(r => r.Balance);

        var grossProfit = Sum(revenues) - Sum(cogs);
        var operatingProfit = grossProfit - Sum(operatingExpenses);
        var profitBeforeTax = operatingProfit + Sum(otherIncome) - Sum(otherExpenses);
        var netProfit = profitBeforeTax - Sum(taxExpenses);

        SortByCode(revenues);
        SortByCode(cogs);
        SortByCode(operatingExpenses);
        SortByCode(otherIncome);
        SortByCode(otherExpenses);
        SortByCode(taxExpenses);

        return new ProfitLossDto(
            filter.CompanyId, range.FromDate, range.ToDate,
            new StatementSectionDto("درآمدهای عملیاتی", revenues, Sum(revenues)),
            new StatementSectionDto("بهای تمام‌شده کالای فروش‌رفته", cogs, Sum(cogs)),
            grossProfit,
            new StatementSectionDto("هزینه‌های عملیاتی", operatingExpenses, Sum(operatingExpenses)),
            operatingProfit,
            new StatementSectionDto("درآمدهای غیرعملیاتی", otherIncome, Sum(otherIncome)),
            new StatementSectionDto("هزینه‌های غیرعملیاتی", otherExpenses, Sum(otherExpenses)),
            profitBeforeTax,
            new StatementSectionDto("مالیات", taxExpenses, Sum(taxExpenses)),
            netProfit);
    }

    private static void SortByCode(List<StatementRowDto> rows)
        => rows.Sort((x, y) => string.CompareOrdinal(x.AccountCode, y.AccountCode));
}

// ===========================================================================
// جریان وجوه نقد — روش مستقیم از دفتر کل (بدون حدس از مانده صندوق)
// ===========================================================================

public interface ICashFlowService
{
    /// <summary>
    /// جریان وجوه نقد روش مستقیم: هر جابه‌جایی حساب وجوه نقد (طبقه‌بندی
    /// CashAndCashEquivalents) به سبد عملیاتی/سرمایه‌گذاری/تأمین مالیِ حساب‌های
    /// مقابلِ همان سند تخصیص داده می‌شود — با اولویت تنظیم صریح
    /// CashFlowClassification و پیش‌فرض طبقه‌بندی گزارش حساب مقابل.
    /// </summary>
    Task<CashFlowDto> GetCashFlowAsync(ReportFilter filter, CancellationToken ct = default);
}

public class CashFlowService : ICashFlowService
{
    private readonly AccountingDbContext _db;
    private readonly IReportClassificationService _classification;

    public CashFlowService(AccountingDbContext db, IReportClassificationService classification)
    {
        _db = db;
        _classification = classification;
    }

    public async Task<CashFlowDto> GetCashFlowAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        var cashAccountIds = await _classification.GetAccountsOfCategoryAsync(
            filter.TenantId, filter.CompanyId, ReportAccountCategory.CashAndCashEquivalents, ct);

        if (cashAccountIds.Count == 0)
        {
            return new CashFlowDto(
                filter.CompanyId, range.FromDate, range.ToDate,
                [new CashFlowBucketDto(CashFlowCategory.Operating, 0m, 0m, 0m),
                 new CashFlowBucketDto(CashFlowCategory.Investing, 0m, 0m, 0m),
                 new CashFlowBucketDto(CashFlowCategory.Financing, 0m, 0m, 0m)],
                0m, 0m,
                CashAccountsConfigured: false,
                Note: "هیچ حسابی با طبقه‌بندی «وجوه نقد و معادل‌ها» تنظیم نشده است؛ ابتدا ریشه حساب‌های صندوق و بانک را در تنظیمات گزارش‌گیری طبقه‌بندی کنید.");
        }

        var cashFilter = filter with { AccountId = null };

        // مانده اول و پایان دوره وجوه نقد
        var opening = await ReportQueryBuilder.Build(_db, cashFilter, range, openingSegment: true)
            .Where(e => cashAccountIds.Contains(e.AccountId))
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .FirstOrDefaultAsync(ct);
        var openingCash = (opening?.Debit ?? 0m) - (opening?.Credit ?? 0m);

        // جابه‌جایی‌های وجوه نقد در بازه (Projection سبک)
        var cashMovements = await ReportQueryBuilder.Build(_db, cashFilter, range, openingSegment: false)
            .Where(e => cashAccountIds.Contains(e.AccountId))
            .Select(e => new { e.DocumentId, Net = e.DebitAmount - e.CreditAmount })
            .ToListAsync(ct);

        var cashNetByDocument = cashMovements
            .GroupBy(m => m.DocumentId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Net));

        if (cashNetByDocument.Count == 0)
        {
            var emptyBuckets = new List<CashFlowBucketDto>
            {
                new(CashFlowCategory.Operating, 0m, 0m, 0m),
                new(CashFlowCategory.Investing, 0m, 0m, 0m),
                new(CashFlowCategory.Financing, 0m, 0m, 0m)
            };
            return new CashFlowDto(filter.CompanyId, range.FromDate, range.ToDate, emptyBuckets,
                openingCash, openingCash, true, null);
        }

        var cashDocumentIds = cashNetByDocument.Keys.ToList();

        // حساب‌های مقابل: سایر سطرهای همان اسناد (غیر نقد)
        var counterRows = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TenantId == filter.TenantId && e.CompanyId == filter.CompanyId
                        && cashDocumentIds.Contains(e.DocumentId)
                        && !cashAccountIds.Contains(e.AccountId))
            .Select(e => new { e.DocumentId, e.AccountId, Gross = e.DebitAmount + e.CreditAmount })
            .ToListAsync(ct);

        var categories = await _classification.ResolveAccountCategoriesAsync(filter.TenantId, filter.CompanyId, ct);
        var overrides = (await _classification.GetCashFlowSettingsAsync(filter.TenantId, filter.CompanyId, ct))
            .ToDictionary(o => o.AccountId, o => o.Category);

        // تخصیص جابه‌جایی نقد هر سند بین حساب‌های مقابل به نسبت مبلغ سطرها
        var allocations = new Dictionary<(CashFlowCategory Bucket, bool IsInflow), decimal>();
        foreach (var (documentId, cashNet) in cashNetByDocument)
        {
            var counterOfDoc = counterRows.Where(r => r.DocumentId == documentId)
                .Where(r => r.Gross != 0m)
                .ToList();
            if (counterOfDoc.Count == 0)
                continue;

            var totalGross = counterOfDoc.Sum(r => r.Gross);
            foreach (var row in counterOfDoc)
            {
                var share = cashNet * (row.Gross / totalGross);
                categories.TryGetValue(row.AccountId, out var category);
                var bucket = _classification.ResolveCashFlowBucket(row.AccountId, category, overrides);
                var key = (bucket, share > 0m);
                allocations[key] = allocations.GetValueOrDefault(key) + Math.Abs(share);
            }
        }

        var buckets = new List<CashFlowBucketDto>
        {
            Build(CashFlowCategory.Operating),
            Build(CashFlowCategory.Investing),
            Build(CashFlowCategory.Financing)
        };

        return new CashFlowDto(
            filter.CompanyId, range.FromDate, range.ToDate, buckets,
            openingCash, openingCash + buckets.Sum(b => b.Net), true, null);

        CashFlowBucketDto Build(CashFlowCategory category)
        {
            var inflow = allocations.GetValueOrDefault((category, true));
            var outflow = allocations.GetValueOrDefault((category, false));
            return new CashFlowBucketDto(category, inflow, outflow, inflow - outflow);
        }
    }
}

// ===========================================================================
// بدهکاران و بستانکاران (تفصیلی اشخاص)
// ===========================================================================

public interface IReceivablesPayablesService
{
    /// <summary>مانده و گردش مشتریان روی حساب‌های دریافتنی (طبقه‌بندی AccountsReceivable).</summary>
    Task<PartyLedgerResultDto> GetReceivablesAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>مانده و گردش تأمین‌کنندگان روی حساب‌های پرداختنی (طبقه‌بندی AccountsPayable).</summary>
    Task<PartyLedgerResultDto> GetPayablesAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>گردش اشخاص — مانده/گردش هر شخص (Party) روی کل حساب‌ها.</summary>
    Task<PartyLedgerResultDto> GetPartyTurnoverAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// گزارش سنی مطالبات — تنها در صورت وجود اطلاعات سررسید معتبر؛ در نبود آن
    /// ساختار گزارش با یادداشت صریح بازمی‌گردد (هیچ عددی بدون منبع ساخته نمی‌شود).
    /// </summary>
    Task<AgingResultDto> GetAgingAsync(ReportFilter filter, CancellationToken ct = default);
}

public class ReceivablesPayablesService : IReceivablesPayablesService
{
    private readonly AccountingDbContext _db;
    private readonly IReportClassificationService _classification;

    public ReceivablesPayablesService(AccountingDbContext db, IReportClassificationService classification)
    {
        _db = db;
        _classification = classification;
    }

    public Task<PartyLedgerResultDto> GetReceivablesAsync(ReportFilter filter, CancellationToken ct = default) =>
        GetPartyLedgerAsync(filter, ReportAccountCategory.AccountsReceivable,
            FinancialDimensionType.Customer, "بدهکاران (مشتریان) — مانده حساب‌های دریافتنی", ct);

    public Task<PartyLedgerResultDto> GetPayablesAsync(ReportFilter filter, CancellationToken ct = default) =>
        GetPartyLedgerAsync(filter, ReportAccountCategory.AccountsPayable,
            FinancialDimensionType.Supplier, "بستانکاران (تأمین‌کنندگان) — مانده حساب‌های پرداختنی", ct);

    public Task<PartyLedgerResultDto> GetPartyTurnoverAsync(ReportFilter filter, CancellationToken ct = default) =>
        GetPartyLedgerAsync(filter, null, FinancialDimensionType.Party, "گردش اشخاص (تفصیلی شخص)", ct);

    private async Task<PartyLedgerResultDto> GetPartyLedgerAsync(
        ReportFilter filter, ReportAccountCategory? accountCategory, FinancialDimensionType dimensionType, string title, CancellationToken ct)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        // محدوده حساب‌ها: طبقه‌بندی تنظیم‌شده، یا همه حساب‌ها برای گردش اشخاص
        List<Guid> accountIds;
        if (accountCategory.HasValue)
        {
            accountIds = await _classification.GetAccountsOfCategoryAsync(
                filter.TenantId, filter.CompanyId, accountCategory.Value, ct);
            if (accountIds.Count == 0)
            {
                return new PartyLedgerResultDto(title, false,
                    "هیچ حسابی با این طبقه‌بندی تنظیم نشده است؛ ریشه حساب‌های دریافتنی/پرداختنی را در تنظیمات گزارش‌گیری تعیین کنید.",
                    [], 0m, 0m);
            }
        }
        else
        {
            accountIds = await ReportQueryBuilder.ResolveAccountIdsAsync(_db, filter, ct);
        }

        var baseQuery = ReportQueryBuilder.Build(_db, filter, range, openingSegment: true)
            .Where(e => accountIds.Contains(e.AccountId));

        // مانده و گردش به تفکیک بعد شخص
        var opening = await (from entry in baseQuery
                             join dim in _db.GeneralLedgerEntryDimensions.AsNoTracking()
                                 on entry.Id equals dim.GeneralLedgerEntryId
                             where dim.TenantId == filter.TenantId
                                   && dim.CompanyId == filter.CompanyId
                                   && dim.DimensionType == dimensionType
                             group new { entry.DebitAmount, entry.CreditAmount } by dim.DimensionId into g
                             select new { DimensionId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        var periodBase = ReportQueryBuilder.Build(_db, filter, range, openingSegment: false)
            .Where(e => accountIds.Contains(e.AccountId));

        var period = await (from entry in periodBase
                            join dim in _db.GeneralLedgerEntryDimensions.AsNoTracking()
                                on entry.Id equals dim.GeneralLedgerEntryId
                            where dim.TenantId == filter.TenantId
                                  && dim.CompanyId == filter.CompanyId
                                  && dim.DimensionType == dimensionType
                            group new { entry.DebitAmount, entry.CreditAmount } by dim.DimensionId into g
                            select new { DimensionId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        // برچسب اشخاص از رجیستری ابعاد
        var dimensionIds = opening.Select(o => o.DimensionId).Union(period.Select(p => p.DimensionId)).ToList();
        var labels = await _db.FinancialDimensions.AsNoTracking()
            .Where(d => d.TenantId == filter.TenantId && d.CompanyId == filter.CompanyId
                        && d.DimensionType == dimensionType && dimensionIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => $"{d.Code} - {d.Name}", ct);

        var openingMap = opening.ToDictionary(o => o.DimensionId, o => (o.Debit, o.Credit));
        var periodMap = period.ToDictionary(p => p.DimensionId, p => (p.Debit, p.Credit));

        var rows = dimensionIds.Select(id =>
        {
            openingMap.TryGetValue(id, out var o);
            periodMap.TryGetValue(id, out var p);
            var openingSigned = o.Debit - o.Credit;
            var closingSigned = openingSigned + (p.Debit - p.Credit);
            return new PartyBalanceRowDto(
                id,
                labels.GetValueOrDefault(id, id.ToString()[..8]),
                Math.Max(openingSigned, 0m), Math.Max(-openingSigned, 0m),
                p.Debit, p.Credit,
                Math.Max(closingSigned, 0m), Math.Max(-closingSigned, 0m),
                closingSigned);
        }).OrderByDescending(r => r.SignedBalance).ToList();

        return new PartyLedgerResultDto(
            title, true, null, rows,
            rows.Sum(r => r.DebitTurnover), rows.Sum(r => r.CreditTurnover));
    }

    public Task<AgingResultDto> GetAgingAsync(ReportFilter filter, CancellationToken ct = default)
    {
        // سررسید مطالبات به منبع اطلاعات سررسید نیاز دارد (فیلد DueDate سند یا
        // قرارداد ماژول فروش/مطالبات). تا آماده شدن آن منبع، گزارش ساختار خود و
        // یادداشت صریح برمی‌گرداند و هیچ عددی ساختگی تولید نمی‌کند.
        var asOf = filter.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var buckets = new List<AgingBucketDto>
        {
            new("جاری (بدون تأخیر)", 0m),
            new("۱ تا ۳۰ روز", 0m),
            new("۳۱ تا ۹۰ روز", 0m),
            new("۹۱ تا ۱۸۰ روز", 0m),
            new("بیش از ۱۸۰ روز", 0m)
        };

        return Task.FromResult<AgingResultDto>(new AgingResultDto(
            "گزارش سنی مطالبات",
            HasReliableDueDates: false,
            Note: "اطلاعات سررسید معتبر (فیلد سررسید اسناد یا قرارداد ماژول مطالبات) در دسترس نیست؛ " +
                  "با افزودن منبع سررسید، سبد‌های سنی از مانده تفصیلی مشتریان محاسبه می‌شوند.",
            asOf, buckets, 0m));
    }
}
