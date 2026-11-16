using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using ERP.BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface ILedgerQueryService
{
    /// <summary>دفتر روزنامه: اسناد قطعی به ترتیب تاریخ و شماره با جمع روزانه و دوره‌ای.</summary>
    Task<JournalReportDto> GetJournalAsync(Guid tenantId, Guid companyId, JournalQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>دفتر کل: خلاصه گردش و مانده حساب‌ها با صفحه‌بندی واقعی.</summary>
    Task<GeneralLedgerReportDto> GetLedgerSummaryAsync(Guid tenantId, Guid companyId, GeneralLedgerQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>گردش تفصیلی یک حساب در دفتر کل (سطر به سطر، صفحه‌بندی واقعی).</summary>
    Task<LedgerTransactionsReportDto> GetLedgerTransactionsAsync(Guid tenantId, Guid companyId, Guid accountId, GeneralLedgerQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>دفتر معین: گردش حساب‌های زیرمجموعه یک حساب کل بدون شمارش دوباره.</summary>
    Task<SubsidiaryLedgerReportDto> GetSubsidiaryLedgerAsync(Guid tenantId, Guid companyId, SubsidiaryLedgerQueryRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// سرویس خواندن گزارش‌های دفتر (Query Side جدا از Write Model).
/// تمام کوئری‌ها AsNoTracking با پروجکشن فقط ستون‌های موردنیاز اجرا می‌شوند و
/// مشخصات حساب‌ها یک‌باره و دسته‌ای بارگذاری می‌شود (بدون N+1).
/// مبنا: فقط اسناد قطعی (Posted) از مسیر سطرهای دفتر کل گزارش می‌شوند؛ اسناد
/// پیش‌نویس هیچ اثری در دفتر روزنامه، دفتر کل، دفتر معین و مانده‌ها ندارند.
/// </summary>
public class LedgerQueryService : ILedgerQueryService
{
    private readonly AccountingDbContext _db;
    private readonly IBalanceEngine _balanceEngine;

    public LedgerQueryService(AccountingDbContext db, IBalanceEngine balanceEngine)
    {
        _db = db;
        _balanceEngine = balanceEngine;
    }

    // ------------------------------------------------------------------
    // Journal (دفتر روزنامه)
    // ------------------------------------------------------------------

    public async Task<JournalReportDto> GetJournalAsync(
        Guid tenantId,
        Guid companyId,
        JournalQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var documentsQuery = BuildJournalDocumentsQuery(tenantId, companyId, request);

        var totalCount = await documentsQuery.CountAsync(cancellationToken);

        var documentRows = await documentsQuery
            .OrderBy(d => d.DocumentDate)
            .ThenBy(d => d.DocumentNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new
            {
                d.Id,
                d.DocumentNumber,
                d.DocumentDate,
                d.Description,
                d.SourceModule,
                d.TotalDebit,
                d.TotalCredit
            })
            .ToListAsync(cancellationToken);

        var pageDocumentIds = documentRows.Select(d => d.Id).ToList();

        // سطرهای گردش صفحه جاری در یک کوئری (بدون N+1)
        var glRows = await _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId && pageDocumentIds.Contains(e.DocumentId))
            .OrderBy(e => e.DocumentId)
            .ThenBy(e => e.LineNumber)
            .Select(e => new
            {
                e.DocumentId,
                e.AccountId,
                e.LineNumber,
                e.Description,
                e.DebitAmount,
                e.CreditAmount
            })
            .ToListAsync(cancellationToken);

        var accountIds = glRows.Select(r => r.AccountId).Distinct().ToList();
        var accountLookup = await LoadAccountLookupAsync(tenantId, companyId, accountIds, cancellationToken);

        var linesByDocument = glRows
            .GroupBy(r => r.DocumentId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r =>
                {
                    var fullCode = accountLookup.TryGetValue(r.AccountId, out var account) ? account.FullCode : string.Empty;
                    var name = accountLookup.TryGetValue(r.AccountId, out var account2) ? account2.Name : string.Empty;
                    return new JournalLineDto(
                        r.AccountId,
                        fullCode,
                        name,
                        r.LineNumber,
                        r.Description,
                        r.DebitAmount,
                        r.CreditAmount);
                }).ToList());

        var entries = documentRows.Select(d => new JournalEntryDto(
            d.Id,
            d.DocumentNumber,
            d.DocumentDate,
            d.Description,
            d.SourceModule,
            d.TotalDebit,
            d.TotalCredit,
            linesByDocument.GetValueOrDefault(d.Id) ?? [])).ToList();

        // جمع روزانه و دوره‌ای روی کل بازه فیلترشده (نه فقط صفحه جاری)
        var dailyRows = await documentsQuery
            .GroupBy(d => d.DocumentDate)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count(),
                Debit = g.Sum(x => x.TotalDebit),
                Credit = g.Sum(x => x.TotalCredit)
            })
            .ToListAsync(cancellationToken);

        var dailyTotals = dailyRows
            .OrderBy(r => r.Date)
            .Select(r => new DailyJournalTotalDto(r.Date, r.Count, r.Debit, r.Credit))
            .ToList();

        var periodTotalDebit = await documentsQuery.SumAsync(d => d.TotalDebit, cancellationToken);
        var periodTotalCredit = await documentsQuery.SumAsync(d => d.TotalCredit, cancellationToken);

        return new JournalReportDto(
            entries,
            dailyTotals,
            periodTotalDebit,
            periodTotalCredit,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    private IQueryable<Domain.Entities.AccountingDocument> BuildJournalDocumentsQuery(Guid tenantId, Guid companyId, JournalQueryRequest request)
    {
        var query = _db.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CompanyId == companyId && d.Status == DocumentStatus.Posted);

        if (request.FiscalYearId.HasValue)
            query = query.Where(d => d.FiscalYearId == request.FiscalYearId.Value);

        if (request.FromDate.HasValue)
            query = query.Where(d => d.DocumentDate >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(d => d.DocumentDate <= request.ToDate.Value);

        if (request.DocumentNumber.HasValue)
            query = query.Where(d => d.DocumentNumber == request.DocumentNumber.Value);

        if (request.BranchId.HasValue)
            query = query.Where(d => d.BranchId == request.BranchId.Value);

        if (request.AccountId.HasValue)
        {
            var accountId = request.AccountId.Value;
            query = query.Where(d => d.Lines.Any(l => l.AccountId == accountId));
        }

        return query;
    }

    // ------------------------------------------------------------------
    // General Ledger summary (دفتر کل)
    // ------------------------------------------------------------------

    public async Task<GeneralLedgerReportDto> GetLedgerSummaryAsync(
        Guid tenantId,
        Guid companyId,
        GeneralLedgerQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, fiscalYearId) = await ResolveWindowAsync(tenantId, companyId, request.FiscalYearId, request.FromDate, request.ToDate, cancellationToken);

        // صفحه‌بندی واقعی روی حساب‌ها (فقط حساب‌های قابل ثبت مگر خلاف آن خواسته شود)
        var accountsQuery = _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId);

        if (!request.IncludeNonPostingAccounts)
            accountsQuery = accountsQuery.Where(a => a.AllowPosting);

        if (request.AccountId.HasValue)
            accountsQuery = accountsQuery.Where(a => a.Id == request.AccountId.Value);

        var totalCount = await accountsQuery.CountAsync(cancellationToken);

        var accountPage = await accountsQuery
            .OrderBy(a => a.FullCode)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new { a.Id, a.FullCode, a.Name, a.NormalBalance })
            .ToListAsync(cancellationToken);

        var pageAccountIds = accountPage.Select(a => a.Id).ToList();

        var (openingByAccount, turnoverByAccount) = await AggregateAsync(
            tenantId, companyId, fiscalYearId, fromDate, toDate, request.BranchId, pageAccountIds, cancellationToken);

        // جمع کل بازه روی همه حساب‌های واجد شرط (نه فقط صفحه جاری)
        var totalIds = await accountsQuery.Select(a => a.Id).ToListAsync(cancellationToken);
        var (allOpening, allTurnover) = await AggregateAsync(
            tenantId, companyId, fiscalYearId, fromDate, toDate, request.BranchId, totalIds, cancellationToken);

        var summaries = accountPage.Select(a =>
        {
            openingByAccount.TryGetValue(a.Id, out var opening);
            turnoverByAccount.TryGetValue(a.Id, out var turnover);
            var closing = opening + turnover.Debit - turnover.Credit;
            return new AccountLedgerSummaryDto(
                a.Id, a.FullCode, a.Name, a.NormalBalance,
                opening, turnover.Debit, turnover.Credit, closing,
                BalanceCalculator.ResolveDirection(closing),
                BalanceCalculator.ResolveDisplayBalance(closing),
                BalanceCalculator.IsAbnormal(a.NormalBalance, closing));
        }).ToList();

        var totalDebit = allTurnover.Values.Sum(t => t.Debit);
        var totalCredit = allTurnover.Values.Sum(t => t.Credit);

        return new GeneralLedgerReportDto(
            fiscalYearId,
            fromDate,
            toDate,
            totalCount,
            request.PageNumber,
            request.PageSize,
            totalDebit,
            totalCredit,
            summaries);
    }

    // ------------------------------------------------------------------
    // Ledger transactions (گردش تفصیلی یک حساب)
    // ------------------------------------------------------------------

    public async Task<LedgerTransactionsReportDto> GetLedgerTransactionsAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        GeneralLedgerQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, fiscalYearId) = await ResolveWindowAsync(tenantId, companyId, request.FiscalYearId, request.FromDate, request.ToDate, cancellationToken);

        var account = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && a.Id == accountId)
            .Select(a => new { a.FullCode, a.Name, a.NormalBalance })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Account", accountId);

        var scoped = new BalanceQueryRequest
        {
            AccountId = accountId,
            FiscalYearId = fiscalYearId,
            FromDate = fromDate,
            ToDate = toDate,
            BranchId = request.BranchId
        };

        var balance = await _balanceEngine.GetBalancesAsync(tenantId, companyId, scoped, cancellationToken);
        var accountBalance = balance.FirstOrDefault(b => b.AccountId == accountId)
            ?? new AccountBalanceResultDto(accountId, account.FullCode, account.Name, account.NormalBalance, 0m, 0m, 0m, 0m,
                LedgerBalanceDirection.None, LedgerBalanceDirection.None, 0m, 0m, false, false);

        var glQuery = _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId && e.AccountId == accountId);

        if (fiscalYearId.HasValue)
            glQuery = glQuery.Where(e => e.FiscalYearId == fiscalYearId.Value);

        if (request.BranchId.HasValue)
            glQuery = glQuery.Where(e => e.BranchId == request.BranchId.Value);

        glQuery = glQuery.Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate);

        var totalCount = await glQuery.CountAsync(cancellationToken);

        var transactions = await glQuery
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.DocumentNumber)
            .ThenBy(e => e.LineNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new LedgerTransactionDto(
                e.Id,
                e.DocumentId,
                e.DocumentNumber,
                e.LineNumber,
                e.EntryDate,
                e.Description,
                e.DebitAmount,
                e.CreditAmount,
                e.CurrencyCode,
                e.SourceModule,
                e.SourceDocumentId))
            .ToListAsync(cancellationToken);

        return new LedgerTransactionsReportDto(
            accountId,
            account.FullCode,
            account.Name,
            accountBalance.OpeningBalance,
            accountBalance.PeriodDebit,
            accountBalance.PeriodCredit,
            accountBalance.ClosingBalance,
            accountBalance.ClosingDirection,
            accountBalance.ClosingDisplayBalance,
            totalCount,
            request.PageNumber,
            request.PageSize,
            transactions);
    }

    // ------------------------------------------------------------------
    // Subsidiary ledger (دفتر معین)
    // ------------------------------------------------------------------

    public async Task<SubsidiaryLedgerReportDto> GetSubsidiaryLedgerAsync(
        Guid tenantId,
        Guid companyId,
        SubsidiaryLedgerQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var parent = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && a.Id == request.ParentAccountId)
            .Select(a => new { a.Id, a.FullCode, a.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Account", request.ParentAccountId);

        // ساختار درختی حساب‌ها یک‌بار بارگذاری و فرزندان حساب کل در حافظه استخراج می‌شوند
        var tree = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new { a.Id, a.ParentAccountId, a.FullCode, a.Name, a.NormalBalance })
            .ToListAsync(cancellationToken);

        var childIdsByParent = tree
            .Where(a => a.ParentAccountId.HasValue)
            .GroupBy(a => a.ParentAccountId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Id).ToList());

        var descendantIds = new List<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parent.Id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childIdsByParent.TryGetValue(current, out var children))
                continue;

            foreach (var childId in children)
            {
                descendantIds.Add(childId);
                queue.Enqueue(childId);
            }
        }

        var (fromDate, toDate, fiscalYearId) = await ResolveWindowAsync(
            tenantId, companyId, request.FiscalYearId, request.FromDate, request.ToDate, cancellationToken);

        var (openingByAccount, turnoverByAccount) = await AggregateAsync(
            tenantId, companyId, fiscalYearId, fromDate, toDate, request.BranchId, descendantIds, cancellationToken);

        var accountLookup = tree.ToDictionary(a => a.Id);

        var subsidiaryRows = descendantIds
            .Where(accountLookup.ContainsKey)
            .Select(id =>
            {
                var account = accountLookup[id];
                openingByAccount.TryGetValue(id, out var opening);
                turnoverByAccount.TryGetValue(id, out var turnover);
                var closing = opening + turnover.Debit - turnover.Credit;
                return (Account: account, Opening: opening, Debit: turnover.Debit, Credit: turnover.Credit, Closing: closing);
            })
            .Where(r => r.Debit != 0m || r.Credit != 0m || r.Opening != 0m)
            .OrderBy(r => r.Account.FullCode, StringComparer.Ordinal)
            .ToList();

        // تضمین عدم شمارش دوباره: هر سطر دفتر کل تنها به یک حساب تعلق دارد؛
        // جمع ردیف‌ها باید دقیقاً برابر تجمیع مستقیم گردش مجموعه فرزندان باشد.
        var summaries = subsidiaryRows.Select(r => new AccountLedgerSummaryDto(
            r.Account.Id, r.Account.FullCode, r.Account.Name, r.Account.NormalBalance,
            r.Opening, r.Debit, r.Credit, r.Closing,
            BalanceCalculator.ResolveDirection(r.Closing),
            BalanceCalculator.ResolveDisplayBalance(r.Closing),
            BalanceCalculator.IsAbnormal(r.Account.NormalBalance, r.Closing))).ToList();

        return new SubsidiaryLedgerReportDto(
            parent.Id,
            parent.FullCode,
            parent.Name,
            fromDate,
            toDate,
            summaries,
            subsidiaryRows.Sum(r => r.Opening),
            subsidiaryRows.Sum(r => r.Debit),
            subsidiaryRows.Sum(r => r.Credit),
            subsidiaryRows.Sum(r => r.Closing));
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private async Task<(DateOnly From, DateOnly To, Guid? FiscalYearId)> ResolveWindowAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (fiscalYearId.HasValue)
        {
            var year = await _db.FiscalYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == fiscalYearId.Value, cancellationToken)
                ?? throw new NotFoundException("FiscalYear", fiscalYearId.Value);

            return (fromDate ?? year.StartDate, toDate ?? year.EndDate, fiscalYearId);
        }

        return (fromDate ?? new DateOnly(1900, 1, 1), toDate ?? new DateOnly(9999, 12, 31), null);
    }

    private async Task<(Dictionary<Guid, decimal> Opening, Dictionary<Guid, (decimal Debit, decimal Credit)> Turnover)> AggregateAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? branchId,
        List<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
            return ([], []);

        var baseQuery = _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId
                        && e.CompanyId == companyId
                        && accountIds.Contains(e.AccountId));

        if (fiscalYearId.HasValue)
            baseQuery = baseQuery.Where(e => e.FiscalYearId == fiscalYearId.Value);

        if (branchId.HasValue)
            baseQuery = baseQuery.Where(e => e.BranchId == branchId.Value);

        var opening = await baseQuery
            .Where(e => e.EntryDate < fromDate)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Opening = g.Sum(x => x.DebitAmount) - g.Sum(x => x.CreditAmount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Opening, cancellationToken);

        var turnoverRows = await baseQuery
            .Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(cancellationToken);

        var turnover = turnoverRows.ToDictionary(r => r.AccountId, r => (r.Debit, r.Credit));

        return (opening, turnover);
    }

    private async Task<Dictionary<Guid, (string FullCode, string Name)>> LoadAccountLookupAsync(
        Guid tenantId,
        Guid companyId,
        List<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
            return [];

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FullCode, a.Name })
            .ToListAsync(cancellationToken);

        return accounts.ToDictionary(a => a.Id, a => (a.FullCode, a.Name));
    }
}
