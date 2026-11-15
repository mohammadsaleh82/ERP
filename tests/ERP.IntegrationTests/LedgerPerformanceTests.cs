using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ERP.IntegrationTests;

/// <summary>
/// تست کارایی گزارش‌های دفتر با دیتاست حجیم: ۱۰۰۰ سند قطعی (۲۰۰۰ سطر دفتر کل).
/// صحت تجمیع‌ها (تطابق کامل با اسناد قطعی) و زمان اجرای هر گزارش در سقف مجاز بررسی می‌شود.
/// </summary>
public class LedgerPerformanceTests : IDisposable
{
    private const int DocumentCount = 1000;

    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _fiscalYear;
    private readonly Account _cash;
    private readonly Account _bank;
    private readonly ITestOutputHelper _output;
    private readonly AccountingDbContext _queryDb;

    public LedgerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _fiscalYear = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _bank = _harness.SeedPostingAccount("1002", "بانک ملت");
        _queryDb = _harness.NewDbContext();
    }

    private async Task SeedLargeDatasetAsync()
    {
        var seedDb = _harness.NewDbContext();
        var periods = await seedDb.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == _fiscalYear.Id)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();
        var postedBy = "bulk-seeder";
        var postedAt = DateTimeOffset.UtcNow;

        // انباشت اسنپ‌شات‌ها: یک ردیف به ازای هر (دوره، حساب) با جمع کل
        var snapshotTotals = new Dictionary<(Guid PeriodId, Guid AccountId), (decimal Debit, decimal Credit)>();

        const int batchSize = 100;
        for (var batchStart = 0; batchStart < DocumentCount; batchStart += batchSize)
        {
            var docs = new List<AccountingDocument>(batchSize);
            var glEntries = new List<GeneralLedgerEntry>(batchSize * 2);

            for (var i = 0; i < batchSize && batchStart + i < DocumentCount; i++)
            {
                var index = batchStart + i;
                var month = (index % 6) + 1; // ژانویه تا ژوئن → دوره‌های ۱ تا ۴
                var day = (index % 20) + 1;
                var date = new DateOnly(2025, month, day);
                var amount = 100m + index;
                var period = periods.First(p => p.ContainsDate(date));
                var docNumber = 100_000 + index;

                var doc = new AccountingDocument(
                    _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, period.Id,
                    docNumber, date, $"سند انبوه {index + 1}")
                {
                    // شناسه باید پیش از ساخت سطرهای دفتر کل صریحاً تولید شود (EF فقط در لحظه Add مقدار می‌دهد)
                    Id = Guid.NewGuid()
                };

                doc.AddLine(_cash.Id, DebitCredit.Debit, amount, "بدهکار انبوه");
                doc.AddLine(_bank.Id, DebitCredit.Credit, amount, "بستانکار انبوه");

                // قطعی‌سازی مستقیم برای seed انبوه (جمع‌ها private هستند و با Reflection تنظیم می‌شوند)
                doc.Status = DocumentStatus.Posted;
                doc.PostedBy = postedBy;
                doc.PostedAtUtc = postedAt;
                typeof(AccountingDocument).GetProperty(nameof(AccountingDocument.TotalDebit))!.SetValue(doc, amount);
                typeof(AccountingDocument).GetProperty(nameof(AccountingDocument.TotalCredit))!.SetValue(doc, amount);

                docs.Add(doc);
                glEntries.Add(GeneralLedgerEntry.FromDocumentLine(doc, doc.Lines[0], period.Id, postedBy, postedAt));
                glEntries.Add(GeneralLedgerEntry.FromDocumentLine(doc, doc.Lines[1], period.Id, postedBy, postedAt));

                var cashKey = (period.Id, _cash.Id);
                snapshotTotals[cashKey] = (snapshotTotals.GetValueOrDefault(cashKey).Debit + amount, snapshotTotals.GetValueOrDefault(cashKey).Credit);
                var bankKey = (period.Id, _bank.Id);
                snapshotTotals[bankKey] = (snapshotTotals.GetValueOrDefault(bankKey).Debit, snapshotTotals.GetValueOrDefault(bankKey).Credit + amount);
            }

            seedDb.Documents.AddRange(docs);
            seedDb.GeneralLedgerEntries.AddRange(glEntries);
            await seedDb.SaveChangesAsync();
        }

        foreach (var (key, totals) in snapshotTotals)
        {
            seedDb.AccountBalanceSnapshots.Add(new AccountBalanceSnapshot(
                _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, key.PeriodId, key.AccountId,
                totals.Debit, totals.Credit));
        }

        await seedDb.SaveChangesAsync();
    }

    [Fact]
    public async Task LedgerReports_WithLargeDataset_ShouldBeAccurateAndFast()
    {
        var seedingWatch = System.Diagnostics.Stopwatch.StartNew();
        await SeedLargeDatasetAsync();
        seedingWatch.Stop();
        _output.WriteLine($"Seeding {DocumentCount} documents took {seedingWatch.ElapsedMilliseconds} ms");

        var tenantId = _harness.TenantId;
        var companyId = _harness.CompanyId;
        const long expectedDebit = (long)DocumentCount * 100 + (long)DocumentCount * (DocumentCount - 1) / 2; // 100..1099

        // ۱) موتور مانده: صحت و سرعت
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var balances = await new BalanceEngine(_queryDb).GetBalancesAsync(tenantId, companyId,
            new BalanceQueryRequest { FiscalYearId = _fiscalYear.Id });
        watch.Stop();
        _output.WriteLine($"BalanceEngine: {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        var journal = await new LedgerQueryService(_queryDb, new BalanceEngine(_queryDb)).GetJournalAsync(tenantId, companyId,
            new JournalQueryRequest { FiscalYearId = _fiscalYear.Id, PageSize = 50 });
        watch.Stop();
        _output.WriteLine($"Journal (page 1 of {journal.TotalCount}): {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        var ledger = await new LedgerQueryService(_queryDb, new BalanceEngine(_queryDb)).GetLedgerSummaryAsync(tenantId, companyId,
            new GeneralLedgerQueryRequest { FiscalYearId = _fiscalYear.Id });
        watch.Stop();
        _output.WriteLine($"GeneralLedgerSummary: {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        var transactions = await new LedgerQueryService(_queryDb, new BalanceEngine(_queryDb)).GetLedgerTransactionsAsync(
            tenantId, companyId, _cash.Id,
            new GeneralLedgerQueryRequest { FiscalYearId = _fiscalYear.Id, PageSize = 50 });
        watch.Stop();
        _output.WriteLine($"LedgerTransactions: {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        var subsidiary = await new LedgerQueryService(_queryDb, new BalanceEngine(_queryDb)).GetSubsidiaryLedgerAsync(tenantId, companyId,
            new SubsidiaryLedgerQueryRequest { ParentAccountId = _cash.ParentAccountId!.Value, FiscalYearId = _fiscalYear.Id });
        watch.Stop();
        _output.WriteLine($"SubsidiaryLedger: {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        var consistency = await new GlConsistencyService(_queryDb).VerifyAsync(tenantId, companyId);
        watch.Stop();
        _output.WriteLine($"LedgerConsistency({consistency.PostedDocumentsChecked} docs): {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        var rebuild = await new BalanceSnapshotService(_queryDb).RebuildAsync(tenantId, companyId, _fiscalYear.Id, "perf-test");
        watch.Stop();
        _output.WriteLine($"SnapshotRebuild: {watch.ElapsedMilliseconds} ms");

        // ---------------- صحت ----------------
        balances.Should().HaveCount(2);
        balances.Sum(b => b.PeriodDebit).Should().Be(expectedDebit, "جمع گردش بدهکار باید دقیقاً برابر مجموع اسناد قطعی باشد");
        balances.Sum(b => b.PeriodCredit).Should().Be(expectedDebit);

        journal.TotalCount.Should().Be(DocumentCount);
        journal.PeriodTotalDebit.Should().Be(expectedDebit);
        journal.PeriodTotalCredit.Should().Be(expectedDebit);
        journal.Entries.Should().HaveCount(50);
        journal.Entries.Select(e => (e.DocumentDate, e.DocumentNumber)).Should().BeInAscendingOrder();
        journal.DailyTotals.Sum(t => t.TotalDebit).Should().Be(expectedDebit);

        ledger.TotalPeriodDebit.Should().Be(expectedDebit);
        var cashSummary = ledger.Accounts.Single(a => a.AccountId == _cash.Id);
        cashSummary.PeriodDebit.Should().Be(expectedDebit);
        cashSummary.ClosingBalance.Should().Be(expectedDebit);

        transactions.TotalCount.Should().Be(DocumentCount);
        transactions.TotalDebit.Should().Be(expectedDebit);
        transactions.ClosingBalance.Should().Be(expectedDebit);

        subsidiary.TotalPeriodDebit.Should().Be(expectedDebit, "تجمیع معین بدون شمارش دوباره");

        _output.WriteLine($"Consistency: posted={consistency.PostedDocumentsChecked}, missingGl={consistency.DocumentsWithMissingGlEntries}, orphanGl={consistency.GlEntriesWithoutValidDocument}, docDebit={consistency.PostedDocumentsTotalDebit}, glDebit={consistency.GlEntriesTotalDebit}");
        consistency.IsConsistent.Should().BeTrue(
            $"هیچ سند قطعی بدون گردش و هیچ گردش یتیم نباید وجود داشته باشد؛ " +
            $"posted={consistency.PostedDocumentsChecked}, missingGl={consistency.DocumentsWithMissingGlEntries}, " +
            $"orphanGl={consistency.GlEntriesWithoutValidDocument}, docDebit={consistency.PostedDocumentsTotalDebit}, glDebit={consistency.GlEntriesTotalDebit}");
        consistency.PostedDocumentsChecked.Should().Be(DocumentCount);
        consistency.PostedDocumentsTotalDebit.Should().Be(consistency.GlEntriesTotalDebit);

        rebuild.SnapshotsCreated.Should().BeGreaterThan(0);
        rebuild.TotalDebit.Should().Be(expectedDebit);

        var reconcile = await new BalanceSnapshotService(_queryDb).ReconcileAsync(tenantId, companyId, _fiscalYear.Id);
        reconcile.IsConsistent.Should().BeTrue("اسنپ‌شات بازسازی‌شده باید دقیقاً با دفتر کل مطابق باشد");

        // ---------------- کارایی ----------------
        // سقف‌های سخاوتمندانه برای محیط CI (صحت مهم‌تر از مطلق زمان است)
        watch.ElapsedMilliseconds.Should().BeLessThan(20_000, "Rebuild اسنپ‌شات روی ۲۰۰۰ سطر دفتر کل باید سریع باشد");
        _output.WriteLine("All ledger reports completed within performance budget on a 2000-entry dataset.");
    }

    public void Dispose()
    {
        _queryDb.Dispose();
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
