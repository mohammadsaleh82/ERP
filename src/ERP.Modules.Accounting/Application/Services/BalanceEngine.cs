using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IBalanceEngine
{
    /// <summary>محاسبه مانده حساب‌ها مستقیماً از گردش‌های دفتر کل (منبع اصلی و معتبر).</summary>
    Task<IReadOnlyList<AccountBalanceResultDto>> GetBalancesAsync(
        Guid tenantId,
        Guid companyId,
        BalanceQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountBalanceResultDto?> GetAccountBalanceAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        BalanceQueryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// موتور محاسبه مانده حساب‌ها.
/// قرارداد علامت: مانده علامت‌دار = بدهکار − بستانکار (مثبت = بدهکار، منفی = بستانکار).
/// محاسبات همیشه از منبع اصلی گردش (acc_gl_entries) انجام می‌شود؛ اسناد پیش‌نویس
/// (Draft) هیچ سطر دفتر کل ندارند و بنابراین هرگز در مانده اثر نمی‌گذارند.
/// محدوده بازه: با سال مالی مشخص، مانده اول دوره از ابتدای سال تا پیش از تاریخ شروع
/// محاسبه می‌شود؛ بدون سال مالی، کل تاریخچه قبل از بازه در نظر گرفته می‌شود.
/// </summary>
public class BalanceEngine : IBalanceEngine
{
    private readonly AccountingDbContext _db;

    public BalanceEngine(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AccountBalanceResultDto>> GetBalancesAsync(
        Guid tenantId,
        Guid companyId,
        BalanceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, fiscalYearId) = await ResolveWindowAsync(tenantId, companyId, request, cancellationToken);

        var openingByAccount = await AggregateOpeningAsync(tenantId, companyId, fiscalYearId, fromDate, request, cancellationToken);
        var turnoverByAccount = await AggregateTurnoverAsync(tenantId, companyId, fiscalYearId, fromDate, toDate, request, cancellationToken);

        var accountIds = openingByAccount.Keys.Union(turnoverByAccount.Keys).ToList();

        if (accountIds.Count == 0)
            return [];

        var accounts = await LoadAccountMetadataAsync(tenantId, companyId, accountIds, cancellationToken);

        var results = new List<AccountBalanceResultDto>(accountIds.Count);
        foreach (var accountId in accountIds)
        {
            if (!accounts.TryGetValue(accountId, out var account))
                continue; // حساب حذف‌شده/نامرئی نباید در گزارش ظاهر شود

            openingByAccount.TryGetValue(accountId, out var opening);
            turnoverByAccount.TryGetValue(accountId, out var turnover);

            results.Add(BalanceCalculator.ToBalanceResult(
                accountId,
                account.FullCode,
                account.Name,
                account.NormalBalance,
                opening,
                turnover.Debit,
                turnover.Credit));
        }

        return results
            .OrderBy(r => r.AccountFullCode, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<AccountBalanceResultDto?> GetAccountBalanceAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        BalanceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var scoped = new BalanceQueryRequest
        {
            AccountId = accountId,
            FiscalYearId = request.FiscalYearId,
            PeriodId = request.PeriodId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            BranchId = request.BranchId
        };

        var balances = await GetBalancesAsync(tenantId, companyId, scoped, cancellationToken);
        if (balances.FirstOrDefault(b => b.AccountId == accountId) is { } balance)
            return balance;

        // حساب بدون گردش: مانده صفر با مشخصات واقعی حساب بازگردانده می‌شود
        var account = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && a.Id == accountId)
            .Select(a => new { a.FullCode, a.Name, a.NormalBalance })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
            return null;

        return BalanceCalculator.ToBalanceResult(
            accountId, account.FullCode, account.Name, account.NormalBalance, 0m, 0m, 0m);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<(DateOnly From, DateOnly To, Guid? FiscalYearId)> ResolveWindowAsync(
        Guid tenantId,
        Guid companyId,
        BalanceQueryRequest request,
        CancellationToken cancellationToken)
    {
        Guid? fiscalYearId = request.FiscalYearId;

        if (request.PeriodId.HasValue)
        {
            var period = await _db.FiscalPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PeriodId.Value, cancellationToken)
                ?? throw new ERP.BuildingBlocks.Exceptions.NotFoundException("FiscalPeriod", request.PeriodId.Value);

            fiscalYearId = period.FiscalYearId;
            return (period.StartDate, period.EndDate, fiscalYearId);
        }

        if (fiscalYearId.HasValue)
        {
            var year = await _db.FiscalYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == fiscalYearId.Value, cancellationToken)
                ?? throw new ERP.BuildingBlocks.Exceptions.NotFoundException("FiscalYear", fiscalYearId.Value);

            return (request.FromDate ?? year.StartDate, request.ToDate ?? year.EndDate, fiscalYearId);
        }

        return (
            request.FromDate ?? new DateOnly(1900, 1, 1),
            request.ToDate ?? new DateOnly(9999, 12, 31),
            null);
    }

    private IQueryable<Domain.Entities.GeneralLedgerEntry> BuildScope(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        BalanceQueryRequest request)
    {
        var query = _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

        if (fiscalYearId.HasValue)
            query = query.Where(e => e.FiscalYearId == fiscalYearId.Value);

        if (request.BranchId.HasValue)
            query = query.Where(e => e.BranchId == request.BranchId.Value);

        if (request.AccountId.HasValue)
            query = query.Where(e => e.AccountId == request.AccountId.Value);

        return query;
    }

    private async Task<Dictionary<Guid, decimal>> AggregateOpeningAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        DateOnly fromDate,
        BalanceQueryRequest request,
        CancellationToken cancellationToken)
    {
        // مانده اول دوره = مانده علامت‌دار تمام گردش‌های پیش از شروع بازه
        return await BuildScope(tenantId, companyId, fiscalYearId, request)
            .Where(e => e.EntryDate < fromDate)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Opening = g.Sum(x => x.DebitAmount) - g.Sum(x => x.CreditAmount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Opening, cancellationToken);
    }

    private async Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> AggregateTurnoverAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        DateOnly fromDate,
        DateOnly toDate,
        BalanceQueryRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await BuildScope(tenantId, companyId, fiscalYearId, request)
            .Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.AccountId, r => (r.Debit, r.Credit));
    }

    private async Task<Dictionary<Guid, (string FullCode, string Name, NormalBalance NormalBalance)>> LoadAccountMetadataAsync(
        Guid tenantId,
        Guid companyId,
        List<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        // بارگذاری یک‌باره مشخصات حساب‌ها (بدون N+1) با پروجکشن فقط ستون‌های موردنیاز
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FullCode, a.Name, a.NormalBalance })
            .ToListAsync(cancellationToken);

        return accounts.ToDictionary(a => a.Id, a => (a.FullCode, a.Name, a.NormalBalance));
    }
}
