using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IDimensionAnalysisService
{
    /// <summary>گردش مالی بر اساس یک نوع بعد مالی (مشتری، تأمین‌کننده، پروژه، مرکز هزینه، شعبه و ...).</summary>
    Task<DimensionTurnoverReportDto> GetTurnoverByDimensionAsync(Guid tenantId, Guid companyId, DimensionTurnoverQuery query, CancellationToken cancellationToken = default);

    /// <summary>گردش یک حساب مشخص به تفکیک یک نوع بعد مالی (مثلاً گردش حساب پرداختنی بر اساس تأمین‌کننده).</summary>
    Task<DimensionTurnoverReportDto> GetAccountTurnoverByDimensionAsync(Guid tenantId, Guid companyId, Guid accountId, DimensionTurnoverQuery query, CancellationToken cancellationToken = default);

    /// <summary>گزارش ترکیبی چندبعدی (تجمیع گردش بر اساس ترکیب چند نوع بعد مالی).</summary>
    Task<CombinedDimensionReportDto> GetCombinedAnalysisAsync(Guid tenantId, Guid companyId, CombinedDimensionQuery query, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Queries / DTOs
// ---------------------------------------------------------------------------

public sealed class DimensionTurnoverQuery : BuildingBlocks.Common.PagedRequest
{
    public FinancialDimensionType DimensionType { get; init; }
    public Guid? DimensionId { get; init; }
    public Guid? FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? BranchId { get; init; }
}

public sealed record DimensionTurnoverRowDto(
    Guid? DimensionId,
    string DimensionCode,
    string DimensionName,
    decimal Debit,
    decimal Credit,
    decimal SignedBalance,
    LedgerBalanceDirection Direction,
    decimal DisplayBalance);

public sealed record DimensionTurnoverReportDto(
    FinancialDimensionType DimensionType,
    Guid? AccountId,
    int TotalCount,
    int PageNumber,
    int PageSize,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<DimensionTurnoverRowDto> Rows);

public sealed class CombinedDimensionQuery : BuildingBlocks.Common.PagedRequest
{
    /// <summary>انواع بعد مالی شرکت‌کننده در گزارش ترکیبی (حداکثر ۳ نوع).</summary>
    public IReadOnlyList<FinancialDimensionType> DimensionTypes { get; init; } = [];
    public Guid? AccountId { get; init; }
    public Guid? FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? BranchId { get; init; }
    /// <summary>سقف ردیف‌های تجمیع‌شده برای کنترل حجم (پیش‌فرض ۵۰۰).</summary>
    public int MaxRows { get; init; } = 500;
}

public sealed record CombinedDimensionRowDto(
    IReadOnlyList<DimensionRefDto> Dimensions,
    decimal Debit,
    decimal Credit,
    decimal SignedBalance,
    LedgerBalanceDirection Direction,
    decimal DisplayBalance);

public sealed record DimensionRefDto(
    FinancialDimensionType DimensionType,
    Guid? DimensionId,
    string Code,
    string Name);

public sealed record CombinedDimensionReportDto(
    IReadOnlyList<FinancialDimensionType> DimensionTypes,
    int TotalMatchedRows,
    bool IsTruncated,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<CombinedDimensionRowDto> Rows);

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------

/// <summary>
/// تحلیل مالی بر اساس ابعاد — Query Side جدا از Write Model.
/// مبنا: سطرهای دفتر کل + جدول انعکاس ابعاد (acc_gl_entry_dimensions) که در همان
/// تراکنش قطعی‌سازی پر شده است. گردش «شعبه» از ستون BranchId خود سطر دفتر کل
/// می‌آید و نیازی به جدول ابعاد ندارد. تمام کوئری‌ها AsNoTracking با پروجکشن سبک
/// اجرا می‌شوند و گزارش ترکیبی با سقف ردیف از انفجار حجم محافظت می‌کند.
/// </summary>
public class DimensionAnalysisService : IDimensionAnalysisService
{
    private const int BranchAnalysisBatchLimit = 50_000;

    private readonly AccountingDbContext _db;

    public DimensionAnalysisService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<DimensionTurnoverReportDto> GetTurnoverByDimensionAsync(Guid tenantId, Guid companyId, DimensionTurnoverQuery query, CancellationToken cancellationToken = default) =>
        await BuildDimensionTurnoverAsync(tenantId, companyId, query, accountId: null, cancellationToken);

    public async Task<DimensionTurnoverReportDto> GetAccountTurnoverByDimensionAsync(Guid tenantId, Guid companyId, Guid accountId, DimensionTurnoverQuery query, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.CompanyId == companyId && a.Id == accountId, cancellationToken);

        if (!exists)
            throw new ERP.BuildingBlocks.Exceptions.NotFoundException("Account", accountId);

        return await BuildDimensionTurnoverAsync(tenantId, companyId, query, accountId, cancellationToken);
    }

    private async Task<DimensionTurnoverReportDto> BuildDimensionTurnoverAsync(
        Guid tenantId,
        Guid companyId,
        DimensionTurnoverQuery query,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        // گزارش شعبه از ستون اختصاصی سطر دفتر کل (بدون جدول ابعاد)
        if (query.DimensionType == FinancialDimensionType.Branch)
            return await BuildBranchTurnoverAsync(tenantId, companyId, query, accountId, cancellationToken);

                var gl = BuildGlScope(tenantId, companyId,
            new GlScopeFilters(query.FiscalYearId, query.FromDate, query.ToDate, query.BranchId), accountId);

        var grouped = await (
                from entryDimension in _db.GeneralLedgerEntryDimensions.AsNoTracking()
                join entry in gl on entryDimension.GeneralLedgerEntryId equals entry.Id
                where entryDimension.TenantId == tenantId
                      && entryDimension.CompanyId == companyId
                      && entryDimension.DimensionType == query.DimensionType
                group new { entry.DebitAmount, entry.CreditAmount } by entryDimension.DimensionId into g
                select new
                {
                    DimensionId = g.Key,
                    Debit = g.Sum(x => x.DebitAmount),
                    Credit = g.Sum(x => x.CreditAmount)
                })
            .ToListAsync(cancellationToken);

        var totalDebit = grouped.Sum(r => r.Debit);
        var totalCredit = grouped.Sum(r => r.Credit);
        var totalCount = grouped.Count;

        var pageRows = grouped
            .OrderByDescending(r => r.Debit + r.Credit)
            .ThenBy(r => r.DimensionId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var metadata = await LoadDimensionMetadataAsync(
            tenantId, companyId, pageRows.Select(r => r.DimensionId).ToList(), cancellationToken);

        var rows = pageRows
            .Select(r =>
            {
                var (code, name) = metadata.GetValueOrDefault(r.DimensionId, ("", ""));
                var signed = r.Debit - r.Credit;
                return new DimensionTurnoverRowDto(
                    r.DimensionId,
                    code,
                    name,
                    r.Debit,
                    r.Credit,
                    signed,
                    BalanceCalculator.ResolveDirection(signed),
                    BalanceCalculator.ResolveDisplayBalance(signed));
            })
            .ToList();

        return new DimensionTurnoverReportDto(
            query.DimensionType,
            accountId,
            totalCount,
            query.PageNumber,
            query.PageSize,
            totalDebit,
            totalCredit,
            rows);
    }

    private async Task<DimensionTurnoverReportDto> BuildBranchTurnoverAsync(
        Guid tenantId,
        Guid companyId,
        DimensionTurnoverQuery query,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
                var gl = BuildGlScope(tenantId, companyId,
            new GlScopeFilters(query.FiscalYearId, query.FromDate, query.ToDate, query.BranchId), accountId);

        // فیلتر به سطرهای دارای شعبه
        var scoped = query.BranchId.HasValue ? gl : gl.Where(e => e.BranchId != null);

        var grouped = await scoped
            .GroupBy(e => e.BranchId!.Value)
            .Select(g => new { BranchId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(cancellationToken);

        var totalDebit = grouped.Sum(r => r.Debit);
        var totalCredit = grouped.Sum(r => r.Credit);
        var totalCount = grouped.Count;

        var pageRows = grouped
            .OrderByDescending(r => r.Debit + r.Credit)
            .ThenBy(r => r.BranchId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var metadata = await LoadBranchMetadataAsync(tenantId, companyId, pageRows.Select(r => r.BranchId).ToList(), cancellationToken);

        var rows = pageRows
            .Select(r =>
            {
                var (code, name) = metadata.GetValueOrDefault(r.BranchId, ("", ""));
                var signed = r.Debit - r.Credit;
                return new DimensionTurnoverRowDto(
                    r.BranchId, code, name, r.Debit, r.Credit, signed,
                    BalanceCalculator.ResolveDirection(signed),
                    BalanceCalculator.ResolveDisplayBalance(signed));
            })
            .ToList();

        return new DimensionTurnoverReportDto(
            FinancialDimensionType.Branch,
            accountId,
            totalCount,
            query.PageNumber,
            query.PageSize,
            totalDebit,
            totalCredit,
            rows);
    }

    public async Task<CombinedDimensionReportDto> GetCombinedAnalysisAsync(Guid tenantId, Guid companyId, CombinedDimensionQuery query, CancellationToken cancellationToken = default)
    {
        var requestedTypes = (query.DimensionTypes ?? [])
            .Distinct()
            .Where(t => t != FinancialDimensionType.Branch)
            .ToList();

        if (requestedTypes.Count == 0 || requestedTypes.Count > 3)
            throw new ERP.BuildingBlocks.Exceptions.ValidationException(
                [new ERP.BuildingBlocks.Results.Error("DimensionAnalysis.InvalidTypes",
                    "برای گزارش ترکیبی چندبعدی باید بین ۱ تا ۳ نوع بعد مالی (غیر از شعبه) انتخاب شود.")]);

        var maxRows = Math.Clamp(query.MaxRows, 1, 5000);

        var gl = BuildGlScope(tenantId, companyId,
            new GlScopeFilters(query.FiscalYearId, query.FromDate, query.ToDate, query.BranchId), query.AccountId)
            .Select(e => new { e.Id, e.DebitAmount, e.CreditAmount });

        var entries = await gl.ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return new CombinedDimensionReportDto(requestedTypes, 0, false, 0m, 0m, []);
        }

        if (entries.Count > BranchAnalysisBatchLimit)
            throw new ERP.BuildingBlocks.Exceptions.ValidationException(
                [new ERP.BuildingBlocks.Results.Error("DimensionAnalysis.DatasetTooLarge",
                    $"حجم داده گزارش ترکیبی بیش از سقف مجاز است ({entries.Count} سطر دفتر کل). لطفاً بازه تاریخ، حساب یا شعبه را محدودتر کنید.")]);

        var entryIds = entries.Select(e => e.Id).ToList();

        // ابعاد فقط برای سطرهای همان مجموعه — کنترل حجم با فیلتر نوع و سطرهای هدف
        var dimensionRows = await (
                from entryDimension in _db.GeneralLedgerEntryDimensions.AsNoTracking()
                join entry in _db.GeneralLedgerEntries.AsNoTracking()
                    on entryDimension.GeneralLedgerEntryId equals entry.Id
                where entryDimension.TenantId == tenantId
                      && entryDimension.CompanyId == companyId
                      && entryIds.Contains(entryDimension.GeneralLedgerEntryId)
                      && requestedTypes.Contains(entryDimension.DimensionType)
                select new { entryDimension.GeneralLedgerEntryId, entryDimension.DimensionType, entryDimension.DimensionId })
            .ToListAsync(cancellationToken);

        var dimensionsByEntry = dimensionRows
            .GroupBy(d => d.GeneralLedgerEntryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var combos = new Dictionary<string, (decimal Debit, decimal Credit, List<DimensionRefDto> Dimensions)>();

        foreach (var entry in entries)
        {
            var entryDimensions = dimensionsByEntry.GetValueOrDefault(entry.Id) ?? [];

            // سطرهایی که همه انواع خواسته‌شده را دارند؛ بقیه از تجمیع ترکیبی حذف می‌شوند
            var byType = entryDimensions
                .GroupBy(d => d.DimensionType)
                .ToDictionary(g => g.Key, g => g.First().DimensionId);

            if (requestedTypes.Any(t => !byType.ContainsKey(t)))
                continue;

            var refs = requestedTypes
                .Select(t => new DimensionRefDto(t, byType[t], string.Empty, string.Empty))
                .ToList();

            var key = string.Join("|", refs.Select(r => r.DimensionType + ":" + r.DimensionId));
            var current = combos.GetValueOrDefault(key);
            combos[key] = (current.Item1 + entry.DebitAmount, current.Item2 + entry.CreditAmount, refs);
        }

        var totalDebit = combos.Values.Sum(c => c.Debit);
        var totalCredit = combos.Values.Sum(c => c.Credit);
        var totalMatched = combos.Count;
        var isTruncated = totalMatched > maxRows;

        var metadataIds = combos.Values.SelectMany(c => c.Dimensions).Select(d => d.DimensionId!.Value).Distinct().ToList();
        var metadata = await LoadDimensionMetadataAsync(tenantId, companyId, metadataIds, cancellationToken);

        var rows = combos.Values
            .OrderByDescending(c => c.Debit + c.Credit)
            .Take(maxRows)
            .Select(c =>
            {
                var filled = c.Dimensions
                    .Select(d => new DimensionRefDto(
                        d.DimensionType,
                        d.DimensionId,
                        metadata.GetValueOrDefault(d.DimensionId!.Value).Item1,
                        metadata.GetValueOrDefault(d.DimensionId!.Value).Item2))
                    .ToList();

                var signed = c.Debit - c.Credit;
                return new CombinedDimensionRowDto(
                    filled,
                    c.Debit,
                    c.Credit,
                    signed,
                    BalanceCalculator.ResolveDirection(signed),
                    BalanceCalculator.ResolveDisplayBalance(signed));
            })
            .ToList();

        return new CombinedDimensionReportDto(requestedTypes, totalMatched, isTruncated, totalDebit, totalCredit, rows);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private sealed record GlScopeFilters(
        Guid? FiscalYearId,
        DateOnly? FromDate,
        DateOnly? ToDate,
        Guid? BranchId);

    private IQueryable<Domain.Entities.GeneralLedgerEntry> BuildGlScope(
        Guid tenantId,
        Guid companyId,
        GlScopeFilters filters,
        Guid? accountId)
    {
        var gl = _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

        if (accountId.HasValue)
            gl = gl.Where(e => e.AccountId == accountId.Value);

        if (filters.FiscalYearId.HasValue)
            gl = gl.Where(e => e.FiscalYearId == filters.FiscalYearId.Value);

        if (filters.FromDate.HasValue)
            gl = gl.Where(e => e.EntryDate >= filters.FromDate.Value);

        if (filters.ToDate.HasValue)
            gl = gl.Where(e => e.EntryDate <= filters.ToDate.Value);

        if (filters.BranchId.HasValue)
            gl = gl.Where(e => e.BranchId == filters.BranchId.Value);

        return gl;
    }

    private async Task<Dictionary<Guid, (string Code, string Name)>> LoadDimensionMetadataAsync(
        Guid tenantId,
        Guid companyId,
        List<Guid> dimensionIds,
        CancellationToken cancellationToken)
    {
        if (dimensionIds.Count == 0)
            return [];

        var rows = await _db.FinancialDimensions
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CompanyId == companyId && dimensionIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Code, d.Name })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => (r.Code, r.Name));
    }

    /// <summary>مشخصات شعبه‌ها از رجیستری ابعاد مالی (نوع Branch) خوانده می‌شود.</summary>
    private async Task<Dictionary<Guid, (string Code, string Name)>> LoadBranchMetadataAsync(
        Guid tenantId,
        Guid companyId,
        List<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        if (branchIds.Count == 0)
            return [];

        var rows = await _db.FinancialDimensions
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId
                        && d.CompanyId == companyId
                        && d.DimensionType == Domain.Enums.FinancialDimensionType.Branch
                        && branchIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Code, d.Name })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => (r.Code, r.Name));
    }
}
