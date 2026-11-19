using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services.Reports;

public interface ITrialBalanceService
{
    /// <summary>
    /// تراز آزمایشی با تجمیع یک تا چهارسطحی، سه برش زمانی (قبل از گردش، گردش دوره،
    /// مانده پایان دوره) و تفکیک بدهکار/بستانکار. منبع همان سطرهای دفتر کل است —
    /// تطابق کامل با دفتر کل به‌صورت ساختاری تضمین می‌شود.
    /// </summary>
    Task<TrialBalanceResultDto> GetTrialBalanceAsync(ReportFilter filter, AccountLevel aggregationLevel, CancellationToken ct = default);
}

/// <summary>موتور تراز آزمایشی.</summary>
public class TrialBalanceService : ITrialBalanceService
{
    private readonly AccountingDbContext _db;

    public TrialBalanceService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<TrialBalanceResultDto> GetTrialBalanceAsync(ReportFilter filter, AccountLevel aggregationLevel, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        // --- مانده قبل از گردش (پیش از شروع بازه) ---
        var openingQuery = ReportQueryBuilder.Build(_db, filter, range, openingSegment: true);
        var opening = await openingQuery
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        // --- گردش طی دوره ---
        var periodQuery = ReportQueryBuilder.Build(_db, filter, range, openingSegment: false);
        var period = await periodQuery
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        // --- تجمیع سطحی: ریشه هر حساب قابل ثبت در سطح درخواستی ---
        var accountIds = opening.Select(o => o.AccountId).Union(period.Select(p => p.AccountId)).ToList();
        var rollup = await BuildRollupAsync(filter, aggregationLevel, accountIds, ct);

        var openingByAccount = opening.ToDictionary(o => o.AccountId, o => (o.Debit, o.Credit));
        var periodByAccount = period.ToDictionary(p => p.AccountId, p => (p.Debit, p.Credit));

        // تجمیع در سطح ریشه‌های درخواستی
        var rowsByRoot = new Dictionary<Guid, (decimal OD, decimal OC, decimal PD, decimal PC)>();
        foreach (var accountId in accountIds.Union(rollup.Keys))
        {
            if (!rollup.TryGetValue(accountId, out var rootAccount))
                continue;
            openingByAccount.TryGetValue(accountId, out var o);
            periodByAccount.TryGetValue(accountId, out var p);
            var (cOD, cOC, cPD, cPC) = rowsByRoot.GetValueOrDefault(rootAccount.Id, (0m, 0m, 0m, 0m));
            rowsByRoot[rootAccount.Id] = (cOD + o.Debit, cOC + o.Credit, cPD + p.Debit, cPC + p.Credit);
        }

        // کلیدهای rowsByRoot شناسه «ریشه» هستند؛ نگاشت ریشه‌ها جدا نگه داشته می‌شود
        var rootAccounts = rollup.Values
            .DistinctBy(v => v.Id)
            .ToDictionary(v => v.Id);

        var rows = rowsByRoot
            .OrderBy(r => rootAccounts[r.Key].Code, StringComparer.Ordinal)
            .Select(r =>
            {
                var rootAccount = rootAccounts[r.Key];
                var openingSigned = r.Value.OD - r.Value.OC;
                var closingSigned = openingSigned + (r.Value.PD - r.Value.PC);
                return new TrialBalanceRow(
                    rootAccount.Id, rootAccount.Code, rootAccount.Name, rootAccount.Level,
                    OpeningDebit: Math.Max(openingSigned, 0m),
                    OpeningCredit: Math.Max(-openingSigned, 0m),
                    PeriodDebit: r.Value.PD,
                    PeriodCredit: r.Value.PC,
                    ClosingDebit: Math.Max(closingSigned, 0m),
                    ClosingCredit: Math.Max(-closingSigned, 0m));
            })
            .ToList();

        var totalDebit = rows.Sum(r => r.ClosingDebit);
        var totalCredit = rows.Sum(r => r.ClosingCredit);

        return new TrialBalanceResultDto(
            filter.CompanyId,
            await ResolveFiscalYearNameAsync(filter, ct),
            range.FromDate,
            range.ToDate,
            aggregationLevel,
            IsBalanced: totalDebit == totalCredit,
            totalDebit,
            totalCredit,
            totalDebit - totalCredit,
            rows);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// نگاشت هر حساب قابل ثبت به «حساب ریشه» در سطح تجمیع درخواستی — با پیشرفت
    /// روی زنجیره والد از خاصیت پیشوندی بودن کدینگ. سطح Detail یعنی بدون تجمیع.
    /// </summary>
    private async Task<Dictionary<Guid, (Guid Id, string Code, string Name, AccountLevel Level)>> BuildRollupAsync(
        ReportFilter filter, AccountLevel targetLevel, List<Guid> accountIds, CancellationToken ct)
    {
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId)
            .Select(a => new { a.Id, a.ParentAccountId, a.FullCode, a.Name, a.Level, a.AllowPosting })
            .ToListAsync(ct);

        var byId = accounts.ToDictionary(a => a.Id);
        var rollup = new Dictionary<Guid, (Guid, string, string, AccountLevel)>();

        foreach (var accountId in accountIds)
        {
            if (!byId.TryGetValue(accountId, out var account))
                continue;

            if (targetLevel == AccountLevel.Detail)
            {
                rollup[account.Id] = (account.Id, account.FullCode, account.Name, account.Level);
                continue;
            }

            var current = account;
            var root = current;
            while (current.ParentAccountId.HasValue && current.Level > targetLevel)
            {
                current = byId[current.ParentAccountId.Value];
                if (current.Level <= targetLevel)
                    root = current;
            }

            if (current.Level <= targetLevel)
                root = current;
            else if (current.Level > targetLevel)
                root = current; // بالاتر از سطح درخواستی (مثلاً حساب بدون والد): خودش ریشه است

            rollup[account.Id] = (root.Id, root.FullCode, root.Name, root.Level);
        }

        return rollup;
    }

    private async Task<string?> ResolveFiscalYearNameAsync(ReportFilter filter, CancellationToken ct)
    {
        if (!filter.FiscalYearId.HasValue)
            return null;
        return await _db.FiscalYears.AsNoTracking()
            .Where(y => y.Id == filter.FiscalYearId.Value)
            .Select(y => y.Name)
            .FirstOrDefaultAsync(ct);
    }
}

// ===========================================================================
// دفاتر: روزنامه، کل، معین، کارت حساب، گردش حساب
// ===========================================================================

public interface ILedgerReportsService
{
    /// <summary>دفتر روزنامه — سطرهای کل سند قطعی به ترتیب تاریخ و شماره سند.</summary>
    Task<PagedResultDto<JournalRowDto>> GetJournalAsync(ReportFilter filter, int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>دفتر کل — جمع حساب‌ها: مانده اول دوره، گردش بدهکار/بستانکار، مانده پایان دوره.</summary>
    Task<List<GeneralLedgerRowDto>> GetGeneralLedgerAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>دفتر معین — ردیف‌های تفصیلی حساب‌های قابل ثبت با شماره سند و شرح.</summary>
    Task<PagedResultDto<SubsidiaryLedgerRowDto>> GetSubsidiaryLedgerAsync(ReportFilter filter, int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>کارت حساب — گردش ردیفی با مانده تراکمی و مانده اول/پایان دوره.</summary>
    Task<AccountCardResultDto> GetAccountCardAsync(Guid accountId, ReportFilter filter, int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>گردش حساب — برش تجمیعی یک حساب.</summary>
    Task<AccountTurnoverDto> GetAccountTurnoverAsync(Guid accountId, ReportFilter filter, CancellationToken ct = default);
}

public class LedgerReportsService : ILedgerReportsService
{
    private readonly AccountingDbContext _db;

    public LedgerReportsService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<JournalRowDto>> GetJournalAsync(ReportFilter filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);
        pageSize = Math.Clamp(pageSize, 1, 500);
        pageNumber = Math.Max(pageNumber, 1);

        var query = ReportQueryBuilder.Build(_db, filter, range, openingSegment: false);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(e => e.EntryDate).ThenBy(e => e.DocumentNumber).ThenBy(e => e.LineNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new JournalRowDto(
                e.DocumentNumber,
                e.EntryDate,
                e.AccountId,
                _db.Accounts.Where(a => a.Id == e.AccountId && a.TenantId == e.TenantId && a.CompanyId == e.CompanyId)
                    .Select(a => a.FullCode).FirstOrDefault() ?? string.Empty,
                _db.Accounts.Where(a => a.Id == e.AccountId && a.TenantId == e.TenantId && a.CompanyId == e.CompanyId)
                    .Select(a => a.Name).FirstOrDefault() ?? string.Empty,
                e.Description,
                e.DebitAmount,
                e.CreditAmount,
                e.CurrencyCode))
            .ToListAsync(ct);

        return new PagedResultDto<JournalRowDto>(rows, total, pageNumber, pageSize);
    }

    public async Task<List<GeneralLedgerRowDto>> GetGeneralLedgerAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        var accountIds = await ReportQueryBuilder.ResolveAccountIdsAsync(_db, filter, ct);

        var opening = await ReportQueryBuilder.Build(_db, filter, range, openingSegment: true)
            .Where(e => accountIds.Contains(e.AccountId))
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        var period = await ReportQueryBuilder.Build(_db, filter, range, openingSegment: false)
            .Where(e => accountIds.Contains(e.AccountId))
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(ct);

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId && accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FullCode, a.Name })
            .ToListAsync(ct);

        var openingMap = opening.ToDictionary(o => o.AccountId, o => (o.Debit, o.Credit));
        var periodMap = period.ToDictionary(p => p.AccountId, p => (p.Debit, p.Credit));

        return accounts
            .OrderBy(a => a.FullCode, StringComparer.Ordinal)
            .Select(a =>
            {
                openingMap.TryGetValue(a.Id, out var o);
                periodMap.TryGetValue(a.Id, out var p);
                var openingSigned = o.Debit - o.Credit;
                var closingSigned = openingSigned + (p.Debit - p.Credit);
                return new GeneralLedgerRowDto(
                    a.Id, a.FullCode, a.Name,
                    Math.Max(openingSigned, 0m), Math.Max(-openingSigned, 0m),
                    p.Debit, p.Credit,
                    Math.Max(closingSigned, 0m), Math.Max(-closingSigned, 0m));
            })
            .Where(r => r.OpeningDebit != 0 || r.OpeningCredit != 0 || r.DebitTurnover != 0 || r.CreditTurnover != 0)
            .ToList();
    }

    public async Task<PagedResultDto<SubsidiaryLedgerRowDto>> GetSubsidiaryLedgerAsync(ReportFilter filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);
        pageSize = Math.Clamp(pageSize, 1, 500);
        pageNumber = Math.Max(pageNumber, 1);

        var query = ReportQueryBuilder.Build(_db, filter, range, openingSegment: false);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(e => e.EntryDate).ThenBy(e => e.DocumentNumber).ThenBy(e => e.LineNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new SubsidiaryLedgerRowDto(
                e.AccountId,
                _db.Accounts.Where(a => a.Id == e.AccountId && a.TenantId == e.TenantId && a.CompanyId == e.CompanyId)
                    .Select(a => a.FullCode).FirstOrDefault() ?? string.Empty,
                _db.Accounts.Where(a => a.Id == e.AccountId && a.TenantId == e.TenantId && a.CompanyId == e.CompanyId)
                    .Select(a => a.Name).FirstOrDefault() ?? string.Empty,
                e.DocumentNumber,
                e.EntryDate,
                e.Description,
                e.DebitAmount,
                e.CreditAmount,
                e.CurrencyCode))
            .ToListAsync(ct);

        return new PagedResultDto<SubsidiaryLedgerRowDto>(rows, total, pageNumber, pageSize);
    }

    public async Task<AccountCardResultDto> GetAccountCardAsync(Guid accountId, ReportFilter filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId && a.Id == accountId, ct)
            ?? throw new NotFoundException("Account", accountId);

        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);

        // Build() فیلتر حساب را اعمال نمی‌کند (در GL با ResolveAccountIds انجام می‌شود)؛
        // کارت حساب باید صریحاً به همان حساب محدود شود.
        var singleAccountFilter = filter with { AccountId = accountId };
        var openingRows = await ReportQueryBuilder.Build(_db, singleAccountFilter, range, openingSegment: true)
            .Where(e => e.AccountId == accountId)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .FirstOrDefaultAsync(ct);
        var openingSigned = (openingRows?.Debit ?? 0m) - (openingRows?.Credit ?? 0m);

        // کارت حساب با مانده تراکمی نیاز به همه ردیف‌های بازه دارد؛ صفحه‌بندی روی
        // خروجی محاسبه‌شده اعمال می‌شود (مانند تراکمی مستقل از صفحه).
        var periodRows = await ReportQueryBuilder.Build(_db, singleAccountFilter, range, openingSegment: false)
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.DocumentNumber).ThenBy(e => e.LineNumber)
            .Select(e => new { e.EntryDate, e.DocumentNumber, e.Description, e.DebitAmount, e.CreditAmount })
            .ToListAsync(ct);

        var running = openingSigned;
        var allRows = periodRows.Select(r =>
        {
            running += r.DebitAmount - r.CreditAmount;
            return new AccountCardRowDto(
                accountId, account.FullCode, account.Name,
                r.EntryDate, r.DocumentNumber, r.Description,
                r.DebitAmount, r.CreditAmount,
                Math.Abs(running),
                running >= 0m ? "Debit" : "Credit");
        }).ToList();

        pageSize = Math.Clamp(pageSize, 1, 500);
        pageNumber = Math.Max(pageNumber, 1);
        var paged = allRows
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AccountCardResultDto(
            accountId, account.FullCode, account.Name,
            range.FromDate, range.ToDate,
            Math.Abs(openingSigned), openingSigned >= 0m ? "Debit" : "Credit",
            paged,
            Math.Abs(running), running >= 0m ? "Debit" : "Credit",
            allRows.Count, pageNumber, pageSize);
    }

    public async Task<AccountTurnoverDto> GetAccountTurnoverAsync(Guid accountId, ReportFilter filter, CancellationToken ct = default)
    {
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == filter.TenantId && a.CompanyId == filter.CompanyId && a.Id == accountId, ct)
            ?? throw new NotFoundException("Account", accountId);

        var range = await ReportRangeResolver.ResolveAsync(_db, filter, ct);
        var singleAccountFilter = filter with { AccountId = accountId };

        var opening = await ReportQueryBuilder.Build(_db, singleAccountFilter, range, openingSegment: true)
            .Where(e => e.AccountId == accountId)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .FirstOrDefaultAsync(ct);

        var period = await ReportQueryBuilder.Build(_db, singleAccountFilter, range, openingSegment: false)
            .Where(e => e.AccountId == accountId)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .FirstOrDefaultAsync(ct);

        var openingSigned = (opening?.Debit ?? 0m) - (opening?.Credit ?? 0m);
        var closingSigned = openingSigned + ((period?.Debit ?? 0m) - (period?.Credit ?? 0m));

        return new AccountTurnoverDto(
            accountId, account.FullCode, account.Name,
            Math.Max(openingSigned, 0m), Math.Max(-openingSigned, 0m),
            period?.Debit ?? 0m, period?.Credit ?? 0m,
            Math.Max(closingSigned, 0m), Math.Max(-closingSigned, 0m));
    }
}
