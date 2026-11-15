using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای اجباری PROMPT 05: دفتر روزنامه، دفتر کل، دفتر معین و موتور مانده.
/// معیار پایان: مجموع گردش‌ها و مانده‌های محاسبه‌شده با اسناد قطعی دقیقاً تطابق داشته باشند.
/// </summary>
public class LedgerReportingIntegrationTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _fiscalYear;
    private readonly Account _cash;
    private readonly Account _bank;
    private readonly Account _payable;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _services;
    private readonly IBalanceEngine _balanceEngine;
    private readonly ILedgerQueryService _ledgerQueries;
    private readonly IBalanceSnapshotService _balanceSnapshotService;
    private readonly IGlConsistencyService _glConsistencyService;

    public LedgerReportingIntegrationTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _fiscalYear = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق", nature: AccountNature.Asset);
        _bank = _harness.SeedPostingAccount("1002", "بانک ملت", nature: AccountNature.Asset);
        _payable = _harness.SeedPostingAccount("2001", "حساب‌های پرداختنی", nature: AccountNature.Liability);
        _services = _harness.NewServices();

        var seedDb = _harness.NewDbContext();
        _balanceEngine = new BalanceEngine(seedDb);
        _ledgerQueries = new LedgerQueryService(_harness.NewDbContext(), _balanceEngine);
        _balanceSnapshotService = new BalanceSnapshotService(_harness.NewDbContext());
        _glConsistencyService = new GlConsistencyService(_harness.NewDbContext());
    }

    /// <summary>ایجاد و قطعی‌سازی یک سند متوازن دوطرفه.</summary>
    private async Task<Guid> PostTwoLineVoucherAsync(
        Account debitAccount,
        Account creditAccount,
        decimal amount,
        DateOnly date,
        string description = "سند تست دفتر")
    {
        var dto = await _services.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, date, description,
            [new VoucherLineCommand(debitAccount.Id, DebitCredit.Debit, amount, "طرف بدهکار"),
             new VoucherLineCommand(creditAccount.Id, DebitCredit.Credit, amount, "طرف بستانکار")],
            DocumentType.General, null, null, null, null, null,
            _harness.CurrentUser.UserId.ToString()));

        var result = await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, dto.Id, null, "poster"));

        return result.DocumentId;
    }

    private BalanceQueryRequest BalanceRequest(Guid? periodId = null, DateOnly? from = null, DateOnly? to = null, Guid? accountId = null) =>
        new() { FiscalYearId = _fiscalYear.Id, PeriodId = periodId, FromDate = from, ToDate = to, AccountId = accountId };

    // ------------------------------------------------------------------
    // سناریوهای ۱ و ۲: گردش بدهکار و گردش بستانکار
    // ------------------------------------------------------------------

    [Fact]
    public async Task DebitAndCreditTurnovers_ShouldExactlyMatchPostedDocuments()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 1_000_000m, new DateOnly(2025, 1, 10));
        await PostTwoLineVoucherAsync(_bank, _cash, 400_000m, new DateOnly(2025, 1, 15));

        var balances = await _balanceEngine.GetBalancesAsync(_harness.TenantId, _harness.CompanyId, BalanceRequest());

        var cash = balances.Single(b => b.AccountId == _cash.Id);
        cash.PeriodDebit.Should().Be(1_000_000m);
        cash.PeriodCredit.Should().Be(400_000m);
        cash.ClosingBalance.Should().Be(600_000m);

        var bank = balances.Single(b => b.AccountId == _bank.Id);
        bank.PeriodDebit.Should().Be(400_000m);
        bank.PeriodCredit.Should().Be(1_000_000m);
        bank.ClosingBalance.Should().Be(-600_000m, "مانده علامت‌دار = بدهکار − بستانکار");

        // تطابق کامل با اسناد قطعی
        var glTotal = await _services.Db.GeneralLedgerEntries.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .SingleAsync();
        glTotal.Debit.Should().Be(1_400_000m);
        glTotal.Credit.Should().Be(1_400_000m);
        balances.Sum(b => b.PeriodDebit).Should().Be(glTotal.Debit);
        balances.Sum(b => b.PeriodCredit).Should().Be(glTotal.Credit);
    }

    // ------------------------------------------------------------------
    // سناریو ۳: مانده اول دوره
    // ------------------------------------------------------------------

    [Fact]
    public async Task OpeningBalance_ShouldEqualClosingOfPriorPeriods()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 700_000m, new DateOnly(2025, 1, 5)); // دوره ۱

        var periods = await _services.Db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == _fiscalYear.Id)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();

        var january = periods.Single(p => p.PeriodNumber == 1);
        var february = periods.Single(p => p.PeriodNumber == 2);

        var februaryBalance = await _balanceEngine.GetBalancesAsync(
            _harness.TenantId, _harness.CompanyId, BalanceRequest(periodId: february.Id));

        var cash = februaryBalance.Single(b => b.AccountId == _cash.Id);
        cash.OpeningBalance.Should().Be(700_000m, "مانده اول دوره اسفند برابر مانده پایان دوره فروردین است");
        cash.PeriodDebit.Should().Be(0m);
        cash.ClosingBalance.Should().Be(700_000m);

        var januaryBalance = await _balanceEngine.GetBalancesAsync(
            _harness.TenantId, _harness.CompanyId, BalanceRequest(periodId: january.Id));
        januaryBalance.Single(b => b.AccountId == _cash.Id).OpeningBalance.Should().Be(0m, "ابتدای سال مالی مانده اول دوره صفر است");
    }

    // ------------------------------------------------------------------
    // سناریو ۴: مانده پایان دوره
    // ------------------------------------------------------------------

    [Fact]
    public async Task ClosingBalance_ShouldBeOpeningPlusDebitMinusCredit()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 100m, new DateOnly(2025, 1, 3));
        await PostTwoLineVoucherAsync(_cash, _bank, 50m, new DateOnly(2025, 2, 4));
        await PostTwoLineVoucherAsync(_bank, _cash, 20m, new DateOnly(2025, 3, 5));

        var balance = await _balanceEngine.GetAccountBalanceAsync(
            _harness.TenantId, _harness.CompanyId, _cash.Id, BalanceRequest());

        balance!.OpeningBalance.Should().Be(0m);
        balance.PeriodDebit.Should().Be(150m);
        balance.PeriodCredit.Should().Be(20m);
        balance.ClosingBalance.Should().Be(130m, "مانده پایان دوره = اول دوره + گردش بدهکار − گردش بستانکار");
    }

    // ------------------------------------------------------------------
    // سناریو ۵: حساب بدون گردش
    // ------------------------------------------------------------------

    [Fact]
    public async Task AccountWithoutTurnover_ShouldReturnZeroBalance()
    {
        var idleAccount = _harness.SeedPostingAccount("1003", "حساب بدون گردش");

        var balance = await _balanceEngine.GetAccountBalanceAsync(
            _harness.TenantId, _harness.CompanyId, idleAccount.Id, BalanceRequest());

        balance.Should().NotBeNull("حساب بدون گردش باید مانده صفر بگیرد، نه نبود نتیجه");
        balance!.OpeningBalance.Should().Be(0m);
        balance.PeriodDebit.Should().Be(0m);
        balance.PeriodCredit.Should().Be(0m);
        balance.ClosingBalance.Should().Be(0m);
        balance.ClosingDirection.Should().Be(LedgerBalanceDirection.None);
    }

    // ------------------------------------------------------------------
    // سناریو ۶: حساب با مانده صفر
    // ------------------------------------------------------------------

    [Fact]
    public async Task ZeroBalanceAccount_ShouldHaveNoneDirection()
    {
        // گردش‌های متقارن روی یک حساب: بدهکار ۳۰۰ و بستانکار ۳۰۰ در یک سند
        var dto = await _services.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, new DateOnly(2025, 1, 12), "سند تسویه کامل",
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 300m, "شارژ صندوق"),
             new VoucherLineCommand(_cash.Id, DebitCredit.Credit, 300m, "برداشت صندوق")],
            DocumentType.General, null, null, null, null, null, "user"));
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, dto.Id, null, "user"));

        var balance = await _balanceEngine.GetAccountBalanceAsync(
            _harness.TenantId, _harness.CompanyId, _cash.Id, BalanceRequest());

        balance!.PeriodDebit.Should().Be(300m);
        balance.PeriodCredit.Should().Be(300m);
        balance.ClosingBalance.Should().Be(0m);
        balance.ClosingDirection.Should().Be(LedgerBalanceDirection.None);
        balance.IsAbnormalClosingBalance.Should().BeFalse("مانده صفر هرگز غیرعادی نیست");
    }

    // ------------------------------------------------------------------
    // سناریو ۷: حساب با مانده منفی (خلاف ماهیت)
    // ------------------------------------------------------------------

    [Fact]
    public async Task NegativeBalance_ShouldBeFlaggedAbnormalWithPreservedSign()
    {
        // حساب پرداختنی (ماهیت بستانکار): بستانکار ۸۰۰ سپس بدهکار ۱٬۳۰۰ → مانده بدهکار ۵۰۰ (خلاف ماهیت)
        await PostTwoLineVoucherAsync(_cash, _payable, 800m, new DateOnly(2025, 1, 8));
        await PostTwoLineVoucherAsync(_payable, _bank, 1_300m, new DateOnly(2025, 1, 20));

        var balance = await _balanceEngine.GetAccountBalanceAsync(
            _harness.TenantId, _harness.CompanyId, _payable.Id, BalanceRequest());

        balance!.ClosingBalance.Should().Be(500m, "مانده علامت‌دار مثبت یعنی مانده بدهکار روی حساب بستانکار");
        balance.ClosingDirection.Should().Be(LedgerBalanceDirection.Debtor);
        balance.ClosingDisplayBalance.Should().Be(500m, "مقدار نمایشی قدرمطلق مانده است");
        balance.IsAbnormalClosingBalance.Should().BeTrue("مانده بدهکار روی حساب با ماهیت بستانکار غیرعادی است");
    }

    // ------------------------------------------------------------------
    // سناریوهای ۸ و ۱۱: تجمیع حساب‌های فرزند و عدم شمارش دوباره
    // ------------------------------------------------------------------

    [Fact]
    public async Task SubsidiaryLedger_ShouldAggregateChildrenWithoutDoubleCounting()
    {
        // دو حساب معین زیر حساب کل «۱۰» با گردش‌های مستقل
        await PostTwoLineVoucherAsync(_cash, _bank, 500m, new DateOnly(2025, 1, 10));
        await PostTwoLineVoucherAsync(_bank, _cash, 200m, new DateOnly(2025, 1, 11));
        await PostTwoLineVoucherAsync(_cash, _payable, 100m, new DateOnly(2025, 1, 12));

        var parentAccount = await _services.Db.Accounts.AsNoTracking()
            .FirstAsync(a => a.Id == _cash.Id);
        var generalAccount = await _services.Db.Accounts.AsNoTracking()
            .FirstAsync(a => a.Id == parentAccount.ParentAccountId!.Value);

        var report = await _ledgerQueries.GetSubsidiaryLedgerAsync(_harness.TenantId, _harness.CompanyId,
            new SubsidiaryLedgerQueryRequest
            {
                ParentAccountId = generalAccount.Id,
                FiscalYearId = _fiscalYear.Id,
                FromDate = new DateOnly(2025, 1, 1),
                ToDate = new DateOnly(2025, 12, 31)
            });

        report.SubsidiaryAccounts.Should().HaveCount(2, "دو حساب معین زیر حساب کل");
        report.TotalPeriodDebit.Should().Be(800m);   // 500 (صندوق) + 200 (بانک) + 100 (صندوق)
        report.TotalPeriodCredit.Should().Be(700m);  // 500 (بانک) + 200 (صندوق)؛ پرداختنی زیر حساب کل دیگری است

        // تضمین عدم شمارش دوباره: جمع فرزندان باید دقیقاً برابر تجمیع مستقیم GL باشد
        var descendantIds = report.SubsidiaryAccounts.Select(s => s.AccountId).ToList();
        var directGl = await _services.Db.GeneralLedgerEntries.AsNoTracking()
            .Where(e => descendantIds.Contains(e.AccountId))
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount), Count = g.Count() })
            .SingleAsync();

        report.TotalPeriodDebit.Should().Be(directGl.Debit);
        report.TotalPeriodCredit.Should().Be(directGl.Credit);

        // هر سطر دفتر کل دقیقاً یک‌بار شمرده شده: ۲ سطر × ۲ سند اول + ۱ سطر صندوق از سند سوم (پرداختنی خارج از این مجموعه)
        directGl.Count.Should().Be(5);
    }

    // ------------------------------------------------------------------
    // سناریو ۹: فیلتر بازه تاریخ
    // ------------------------------------------------------------------

    [Fact]
    public async Task DateRangeFilter_ShouldExcludeOutsideDocuments()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 1_000m, new DateOnly(2025, 1, 10));
        await PostTwoLineVoucherAsync(_cash, _bank, 2_000m, new DateOnly(2025, 2, 10));

        var februaryBalance = await _balanceEngine.GetBalancesAsync(
            _harness.TenantId, _harness.CompanyId,
            new BalanceQueryRequest { FiscalYearId = _fiscalYear.Id, FromDate = new DateOnly(2025, 2, 1), ToDate = new DateOnly(2025, 2, 28) });

        var cash = februaryBalance.Single(b => b.AccountId == _cash.Id);
        cash.OpeningBalance.Should().Be(1_000m, "گردش فروردین در مانده اول دوره اسفند ظاهر می‌شود");
        cash.PeriodDebit.Should().Be(2_000m, "تنها گردش داخل بازه شمرده می‌شود");

        var januaryOnly = await _balanceEngine.GetBalancesAsync(
            _harness.TenantId, _harness.CompanyId,
            new BalanceQueryRequest { FiscalYearId = _fiscalYear.Id, FromDate = new DateOnly(2025, 1, 1), ToDate = new DateOnly(2025, 1, 31) });
        januaryOnly.Single(b => b.AccountId == _cash.Id).PeriodDebit.Should().Be(1_000m);
    }

    // ------------------------------------------------------------------
    // سناریو ۱۰: چند دوره مالی
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultiplePeriods_ShouldCarryBalancesForward()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 600m, new DateOnly(2025, 1, 15)); // دوره ۱
        await PostTwoLineVoucherAsync(_cash, _bank, 300m, new DateOnly(2025, 2, 15)); // دوره ۲
        await PostTwoLineVoucherAsync(_bank, _cash, 100m, new DateOnly(2025, 3, 15)); // دوره ۳

        var periods = await _services.Db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == _fiscalYear.Id)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();

        var periodBalances = new List<AccountBalanceResultDto>();
        foreach (var period in periods.Take(3))
        {
            var rows = await _balanceEngine.GetBalancesAsync(
                _harness.TenantId, _harness.CompanyId, BalanceRequest(periodId: period.Id));
            periodBalances.Add(rows.Single(b => b.AccountId == _cash.Id));
        }

        periodBalances[0].ClosingBalance.Should().Be(600m);
        periodBalances[1].OpeningBalance.Should().Be(600m, "مانده اول دوره دوم ادامه دوره اول است");
        periodBalances[1].ClosingBalance.Should().Be(900m);
        periodBalances[2].OpeningBalance.Should().Be(900m);
        periodBalances[2].PeriodCredit.Should().Be(100m);
        periodBalances[2].ClosingBalance.Should().Be(800m);
    }

    // ------------------------------------------------------------------
    // دفتر روزنامه: ترتیب، جمع روزانه، جمع دوره‌ای، فیلتر حساب و صفحه‌بندی
    // ------------------------------------------------------------------

    [Fact]
    public async Task Journal_ShouldOrderByDateAndNumber_WithDailyAndPeriodTotals()
    {
        var doc1 = await PostTwoLineVoucherAsync(_cash, _bank, 100m, new DateOnly(2025, 1, 5), "سند اول");
        var doc2 = await PostTwoLineVoucherAsync(_cash, _bank, 200m, new DateOnly(2025, 1, 5), "سند دوم");
        var doc3 = await PostTwoLineVoucherAsync(_cash, _bank, 300m, new DateOnly(2025, 1, 8), "سند سوم");

        var journal = await _ledgerQueries.GetJournalAsync(_harness.TenantId, _harness.CompanyId,
            new JournalQueryRequest { FiscalYearId = _fiscalYear.Id, PageSize = 2 });

        journal.TotalCount.Should().Be(3);
        journal.Entries.Should().HaveCount(2, "صفحه‌بندی واقعی روی اسناد");
        journal.Entries.Select(e => e.DocumentNumber).Should().BeInAscendingOrder();
        journal.PeriodTotalDebit.Should().Be(600m, "جمع دوره‌ای روی کل بازه است نه فقط صفحه جاری");
        journal.PeriodTotalCredit.Should().Be(600m);

        journal.DailyTotals.Should().HaveCount(2, "دو روز متمایز");
        journal.DailyTotals[0].Date.Should().Be(new DateOnly(2025, 1, 5));
        journal.DailyTotals[0].DocumentsCount.Should().Be(2);
        journal.DailyTotals[0].TotalDebit.Should().Be(300m);
        journal.DailyTotals[1].Date.Should().Be(new DateOnly(2025, 1, 8));
        journal.DailyTotals[1].TotalDebit.Should().Be(300m);

        journal.Entries.Single(e => e.DocumentId == doc1).Lines.Should().HaveCount(2);
        journal.Entries.Should().OnlyContain(e => e.Lines.Count == 2 && e.Lines.All(l => !string.IsNullOrEmpty(l.AccountFullCode)));

        // فیلتر شماره سند و فیلتر حساب
        var byNumber = await _ledgerQueries.GetJournalAsync(_harness.TenantId, _harness.CompanyId,
            new JournalQueryRequest { DocumentNumber = journal.Entries[0].DocumentNumber });
        byNumber.TotalCount.Should().Be(1);

        var byAccount = await _ledgerQueries.GetJournalAsync(_harness.TenantId, _harness.CompanyId,
            new JournalQueryRequest { AccountId = _bank.Id });
        byAccount.TotalCount.Should().Be(3, "همه اسناد حساب بانک را دارند");

        _ = doc2;
        _ = doc3;
    }

    // ------------------------------------------------------------------
    // تغییرناپذیری Posted Ledger
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostedLedgerEntries_ShouldRejectUpdateAndDelete()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 250m, new DateOnly(2025, 1, 10));

        // تلاش برای ویرایش سطر دفتر کل
        using (var tamperDb = _harness.NewDbContext())
        {
            var entry = await tamperDb.GeneralLedgerEntries.FirstAsync();
            entry.DebitAmount = 999_999m;

            var updateAction = async () => await tamperDb.SaveChangesAsync();
            (
                    await updateAction.Should().ThrowAsync<AccountingDomainException>())
                .Which.Code.Should().Be("Accounting.PostedLedgerImmutable");
        }

        // تلاش برای حذف سطر دفتر کل
        using (var deleteDb = _harness.NewDbContext())
        {
            var entry = await deleteDb.GeneralLedgerEntries.FirstAsync();
            deleteDb.GeneralLedgerEntries.Remove(entry);

            var deleteAction = async () => await deleteDb.SaveChangesAsync();
            (
                    await deleteAction.Should().ThrowAsync<AccountingDomainException>())
                .Which.Code.Should().Be("Accounting.PostedLedgerImmutable");
        }
    }

    // ------------------------------------------------------------------
    // سناریو ۱۲: Reconciliation با اسناد قطعی + اسنپ‌شات‌ها
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reconciliation_WithPostedDocuments_ShouldDetectDriftAndRepair()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 1_500m, new DateOnly(2025, 1, 10));
        await PostTwoLineVoucherAsync(_payable, _bank, 900m, new DateOnly(2025, 2, 10));

        // ۱) تطابق اسناد قطعی با گردش‌های دفتر کل
        var consistency = await _glConsistencyService.VerifyAsync(_harness.TenantId, _harness.CompanyId);
        consistency.IsConsistent.Should().BeTrue();
        consistency.PostedDocumentsChecked.Should().Be(2);
        consistency.DocumentsWithMissingGlEntries.Should().Be(0);
        consistency.GlEntriesWithoutValidDocument.Should().Be(0);
        consistency.PostedDocumentsTotalDebit.Should().Be(consistency.GlEntriesTotalDebit);
        consistency.PostedDocumentsTotalCredit.Should().Be(consistency.GlEntriesTotalCredit);

        // ۲) اسنپ‌شات‌ها (به‌روزشده در تراکنش Posting) با دفتر کل سازگارند
        var reconciliation = await _balanceSnapshotService.ReconcileAsync(_harness.TenantId, _harness.CompanyId, _fiscalYear.Id);
        reconciliation.IsConsistent.Should().BeTrue("اسنپ‌شات در همان تراکنش Posting به‌روز شده است");
        reconciliation.ComparedRows.Should().BeGreaterThan(0);

        // ۳) شبیه‌سازی انحراف (Drift): حذف یک ردیف اسنپ‌شات خارج از مسیر مجاز
        using (var driftDb = _harness.NewDbContext())
        {
            var snapshot = await driftDb.AccountBalanceSnapshots.FirstAsync();
            driftDb.AccountBalanceSnapshots.Remove(snapshot);
            await driftDb.SaveChangesAsync();
        }

        var drifted = await _balanceSnapshotService.ReconcileAsync(_harness.TenantId, _harness.CompanyId, _fiscalYear.Id);
        drifted.IsConsistent.Should().BeFalse("انحراف اسنپ‌شات باید در مطابقت‌سنجی کشف شود");
        drifted.MismatchedRows.Should().BeGreaterThan(0);

        // ۴) بازسازی کامل از منبع اصلی → سازگاری دوباره
        var rebuild = await _balanceSnapshotService.RebuildAsync(_harness.TenantId, _harness.CompanyId, _fiscalYear.Id, "admin");
        rebuild.SnapshotsCreated.Should().BeGreaterThan(0);

        var repaired = await _balanceSnapshotService.ReconcileAsync(_harness.TenantId, _harness.CompanyId, _fiscalYear.Id);
        repaired.IsConsistent.Should().BeTrue("Rebuild اسنپ‌شات را با دفتر کل منطبق کرده است");

        // ۵) مانده‌های اسنپ‌شات با مانده‌های زنده یکسان‌اند
        var liveBalances = await _balanceEngine.GetBalancesAsync(_harness.TenantId, _harness.CompanyId, BalanceRequest(periodId: null, from: new DateOnly(2025, 1, 1), to: new DateOnly(2025, 12, 31)));
        var snapshotBalances = await _balanceSnapshotService.GetSnapshotBalancesAsync(_harness.TenantId, _harness.CompanyId, _fiscalYear.Id, null);
        snapshotBalances.Should().HaveCount(liveBalances.Count);
        foreach (var live in liveBalances)
        {
            var snapshot = snapshotBalances.Single(s => s.AccountId == live.AccountId);
            snapshot.PeriodDebit.Should().Be(live.PeriodDebit);
            snapshot.PeriodCredit.Should().Be(live.PeriodCredit);
        }
    }

    // ------------------------------------------------------------------
    // دفتر کل: خلاصه و گردش تفصیلی
    // ------------------------------------------------------------------

    [Fact]
    public async Task GeneralLedgerSummaryAndTransactions_ShouldMatchBalances()
    {
        await PostTwoLineVoucherAsync(_cash, _bank, 2_500m, new DateOnly(2025, 1, 10));
        await PostTwoLineVoucherAsync(_cash, _bank, 1_500m, new DateOnly(2025, 2, 10));

        var summary = await _ledgerQueries.GetLedgerSummaryAsync(_harness.TenantId, _harness.CompanyId,
            new GeneralLedgerQueryRequest { FiscalYearId = _fiscalYear.Id });

        summary.TotalPeriodDebit.Should().Be(4_000m);
        summary.TotalPeriodCredit.Should().Be(4_000m);
        summary.Accounts.Single(a => a.AccountId == _cash.Id).PeriodDebit.Should().Be(4_000m);
        summary.Accounts.Single(a => a.AccountId == _bank.Id).PeriodCredit.Should().Be(4_000m);
        summary.Accounts.Sum(a => a.PeriodDebit).Should().Be(4_000m);

        var cashSummary = summary.Accounts.Single(a => a.AccountId == _cash.Id);
        cashSummary.PeriodDebit.Should().Be(4_000m);
        cashSummary.ClosingBalance.Should().Be(4_000m);

        var transactions = await _ledgerQueries.GetLedgerTransactionsAsync(_harness.TenantId, _harness.CompanyId, _cash.Id,
            new GeneralLedgerQueryRequest { FiscalYearId = _fiscalYear.Id });

        transactions.TotalCount.Should().Be(2);
        transactions.TotalDebit.Should().Be(4_000m);
        transactions.OpeningBalance.Should().Be(0m);
        transactions.ClosingBalance.Should().Be(4_000m);
        transactions.Transactions.Select(t => t.DocumentNumber).Should().BeInAscendingOrder();
        transactions.Transactions.Should().OnlyContain(t => t.SourceModule == null, "اسناد تست دستی بدون منشأ سیستمی‌اند");
    }

    public void Dispose() => _harness.Dispose();
}
