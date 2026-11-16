using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IBalanceSnapshotService
{
    /// <summary>
    /// انبارش گردش‌های تازه روی اسنپ‌شات‌ها «داخل تراکنش جاری Posting» تا اسنپ‌شات همیشه
    /// با دفتر کل سازگار بماند. در برخورد هم‌زمانی با Reload و تلاش مجدد ادامه می‌دهد.
    /// </summary>
    Task ApplyAsync(
        AccountingDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid fiscalPeriodId,
        IReadOnlyList<(Guid AccountId, decimal Debit, decimal Credit)> movements,
        CancellationToken cancellationToken = default);

    /// <summary>بازسازی کامل اسنپ‌شات‌ها از منبع اصلی (دفتر کل) برای سال مالی مشخص یا کل شرکت.</summary>
    Task<SnapshotRebuildResultDto> RebuildAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        string performedBy,
        CancellationToken cancellationToken = default);

    /// <summary>مطابقت‌سنجی اسنپ‌شات‌ها با تجمیع زنده دفتر کل (Reconciliation).</summary>
    Task<BalanceReconciliationResultDto> ReconcileAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        CancellationToken cancellationToken = default);

    /// <summary>مانده‌های خواندنی سریع از اسنپ‌شات‌ها برای یک دوره مالی.</summary>
    Task<IReadOnlyList<AccountBalanceResultDto>> GetSnapshotBalancesAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid? periodId,
        CancellationToken cancellationToken = default);
}

public class BalanceSnapshotService : IBalanceSnapshotService
{
    private readonly AccountingDbContext _db;

    public BalanceSnapshotService(AccountingDbContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------
    // In-transaction apply (قلم نوشتن)
    // ------------------------------------------------------------------

    public async Task ApplyAsync(
        AccountingDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid fiscalPeriodId,
        IReadOnlyList<(Guid AccountId, decimal Debit, decimal Credit)> movements,
        CancellationToken cancellationToken = default)
    {
        var aggregated = movements
            .Where(m => m.Debit != 0m || m.Credit != 0m)
            .GroupBy(m => m.AccountId)
            .Select(g => (AccountId: g.Key, Debit: g.Sum(x => x.Debit), Credit: g.Sum(x => x.Credit)))
            .ToList();

        if (aggregated.Count == 0)
            return;

        const int maxRetries = 10;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await StageAccumulationsAsync(db, tenantId, companyId, fiscalYearId, fiscalPeriodId, aggregated, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt == maxRetries - 1)
                    throw new AccountingDomainException("Accounting.SnapshotConcurrencyConflict",
                        "به‌روزرسانی هم‌زمان اسنپ‌شات مانده حساب‌ها با تضاد برخورد کرد. لطفاً مجدداً تلاش فرمایید.");

                // بارگذاری مجدد مقادیر واقعی پایگاه‌داده و تلاش مجدد انبارش
                foreach (var entry in db.ChangeTracker.Entries<AccountBalanceSnapshot>()
                             .Where(e => e.State == EntityState.Modified))
                {
                    await entry.ReloadAsync(cancellationToken);
                }

                await Task.Delay(Random.Shared.Next(10, 40), cancellationToken);
            }
        }
    }

    private async Task StageAccumulationsAsync(
        AccountingDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid fiscalPeriodId,
        List<(Guid AccountId, decimal Debit, decimal Credit)> aggregated,
        CancellationToken cancellationToken)
    {
        var accountIds = aggregated.Select(a => a.AccountId).ToList();

        var existing = await db.AccountBalanceSnapshots
            .Where(s => s.TenantId == tenantId
                        && s.CompanyId == companyId
                        && s.FiscalPeriodId == fiscalPeriodId
                        && accountIds.Contains(s.AccountId))
            .ToListAsync(cancellationToken);

        var byAccount = existing.ToDictionary(s => s.AccountId);

        foreach (var movement in aggregated)
        {
            if (byAccount.TryGetValue(movement.AccountId, out var snapshot))
            {
                snapshot.Accumulate(movement.Debit, movement.Credit);
            }
            else
            {
                db.AccountBalanceSnapshots.Add(new AccountBalanceSnapshot(
                    tenantId, companyId, fiscalYearId, fiscalPeriodId, movement.AccountId,
                    movement.Debit, movement.Credit));
            }
        }
    }

    // ------------------------------------------------------------------
    // Rebuild (بازسازی از منبع اصلی)
    // ------------------------------------------------------------------

    public async Task<SnapshotRebuildResultDto> RebuildAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var aggregated = await AggregateLedgerAsync(tenantId, companyId, fiscalYearId, cancellationToken);

        var existingQuery = _db.AccountBalanceSnapshots
            .Where(s => s.TenantId == tenantId && s.CompanyId == companyId);

        if (fiscalYearId.HasValue)
            existingQuery = existingQuery.Where(s => s.FiscalYearId == fiscalYearId.Value);

        var existing = await existingQuery.ToListAsync(cancellationToken);
        _db.AccountBalanceSnapshots.RemoveRange(existing);

        foreach (var row in aggregated)
        {
            _db.AccountBalanceSnapshots.Add(new AccountBalanceSnapshot(
                tenantId, companyId, row.FiscalYearId, row.FiscalPeriodId, row.AccountId,
                row.Debit, row.Credit));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new SnapshotRebuildResultDto(
            SnapshotsCreated: aggregated.Count,
            AccountsCovered: aggregated.Select(a => a.AccountId).Distinct().Count(),
            TotalDebit: aggregated.Sum(a => a.Debit),
            TotalCredit: aggregated.Sum(a => a.Credit));
    }

    // ------------------------------------------------------------------
    // Reconcile (مطابقت‌سنجی با دفتر کل)
    // ------------------------------------------------------------------

    public async Task<BalanceReconciliationResultDto> ReconcileAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var ledger = await AggregateLedgerAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        var ledgerLookup = ledger.ToDictionary(r => (r.FiscalPeriodId, r.AccountId));

        var snapshotQuery = _db.AccountBalanceSnapshots
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CompanyId == companyId);

        if (fiscalYearId.HasValue)
            snapshotQuery = snapshotQuery.Where(s => s.FiscalYearId == fiscalYearId.Value);

        var snapshots = await snapshotQuery.ToListAsync(cancellationToken);

        var mismatches = new List<BalanceReconciliationRowDto>();
        var compared = new HashSet<(Guid, Guid)>();

        foreach (var snapshot in snapshots)
        {
            compared.Add((snapshot.FiscalPeriodId, snapshot.AccountId));
            ledgerLookup.TryGetValue((snapshot.FiscalPeriodId, snapshot.AccountId), out var expected);

            var debitDiff = snapshot.PeriodDebit - expected.Debit;
            var creditDiff = snapshot.PeriodCredit - expected.Credit;

            if (debitDiff != 0m || creditDiff != 0m)
            {
                mismatches.Add(new BalanceReconciliationRowDto(
                    snapshot.AccountId, snapshot.FiscalPeriodId,
                    snapshot.PeriodDebit, snapshot.PeriodCredit,
                    expected.Debit, expected.Credit, debitDiff, creditDiff));
            }
        }

        // گردش‌های دفتر کل که اسنپ‌شاتی برای آن‌ها وجود ندارد
        foreach (var ledgerRow in ledger)
        {
            if (compared.Contains((ledgerRow.FiscalPeriodId, ledgerRow.AccountId)))
                continue;

            mismatches.Add(new BalanceReconciliationRowDto(
                ledgerRow.AccountId, ledgerRow.FiscalPeriodId,
                0m, 0m, ledgerRow.Debit, ledgerRow.Credit,
                -ledgerRow.Debit, -ledgerRow.Credit));
        }

        return new BalanceReconciliationResultDto(
            IsConsistent: mismatches.Count == 0,
            ComparedRows: compared.Count,
            MismatchedRows: mismatches.Count,
            mismatches.Take(100).ToList());
    }

    // ------------------------------------------------------------------
    // Snapshot reads (مسیر خواندن سریع)
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<AccountBalanceResultDto>> GetSnapshotBalancesAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid? periodId,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _db.AccountBalanceSnapshots
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CompanyId == companyId && s.FiscalYearId == fiscalYearId)
            .Select(s => new { s.FiscalPeriodId, s.AccountId, s.PeriodDebit, s.PeriodCredit })
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
            return [];

        var periodOrder = await _db.FiscalPeriods
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.CompanyId == companyId && p.FiscalYearId == fiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .Select(p => new { p.Id, p.PeriodNumber })
            .ToListAsync(cancellationToken);

        var periodRank = periodOrder.Select((p, index) => (p.Id, Rank: index))
            .ToDictionary(x => x.Id, x => x.Rank);

        var accountIds = snapshots.Select(s => s.AccountId).Distinct().ToList();

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FullCode, a.Name, a.NormalBalance })
            .ToListAsync(cancellationToken);
        var accountLookup = accounts.ToDictionary(a => a.Id);

        // مانده اول دوره = مجموع گردش دوره‌های قبل از دوره هدف (اسنپ‌شات‌ها)
        var maxRank = periodId.HasValue ? periodRank.GetValueOrDefault(periodId.Value, periodRank.Count) : periodRank.Count;

        var balances = new Dictionary<Guid, (decimal Opening, decimal Debit, decimal Credit)>();
        foreach (var s in snapshots)
        {
            if (!periodRank.TryGetValue(s.FiscalPeriodId, out var rank))
                continue;

            balances.TryGetValue(s.AccountId, out var current);
            var opening = current.Opening;
            var debit = current.Debit;
            var credit = current.Credit;

            if (rank < maxRank)
                opening += s.PeriodDebit - s.PeriodCredit;

            if (!periodId.HasValue || rank == maxRank)
            {
                debit += s.PeriodDebit;
                credit += s.PeriodCredit;
            }

            balances[s.AccountId] = (opening, debit, credit);
        }

        var results = new List<AccountBalanceResultDto>(balances.Count);
        foreach (var (accountId, value) in balances)
        {
            if (!accountLookup.TryGetValue(accountId, out var account))
                continue;

            results.Add(BalanceCalculator.ToBalanceResult(
                accountId, account.FullCode, account.Name, account.NormalBalance,
                value.Opening, value.Debit, value.Credit));
        }

        return results.OrderBy(r => r.AccountFullCode, StringComparer.Ordinal).ToList();
    }

    // ------------------------------------------------------------------
    // Shared aggregation
    // ------------------------------------------------------------------

    private async Task<List<(Guid FiscalYearId, Guid FiscalPeriodId, Guid AccountId, decimal Debit, decimal Credit)>> AggregateLedgerAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId,
        CancellationToken cancellationToken)
    {
        var query = _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

        if (fiscalYearId.HasValue)
            query = query.Where(e => e.FiscalYearId == fiscalYearId.Value);

        var rows = await query
            .GroupBy(e => new { e.FiscalYearId, e.FiscalPeriodId, e.AccountId })
            .Select(g => new
            {
                g.Key.FiscalYearId,
                g.Key.FiscalPeriodId,
                g.Key.AccountId,
                Debit = g.Sum(x => x.DebitAmount),
                Credit = g.Sum(x => x.CreditAmount)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.FiscalYearId, r.FiscalPeriodId, r.AccountId, r.Debit, r.Credit))
            .ToList();
    }
}
