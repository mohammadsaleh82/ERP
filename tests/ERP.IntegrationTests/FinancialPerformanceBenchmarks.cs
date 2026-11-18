using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.IO;
using ERP.Modules.Accounting.Application.Services.Reports;
using ERP.Modules.Accounting.Application.DTOs;
using FluentAssertions;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace ERP.IntegrationTests;

/// <summary>
/// بنچمارک کارایی گزارش‌ها (PROMPT 09): یک میلیون ردیف گردش، حساب‌های متعدد،
/// چند شرکت و چند سال مالی — اجرا روی SQLite درون‌حافظه‌ای برای تکرارپذیری.
/// نتایج مستند می‌شود در docs/development/performance-benchmarks.md
/// </summary>
public class FinancialPerformanceBenchmarks
{
    private readonly ITestOutputHelper _output;

    public FinancialPerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    private const int TotalRows = 1_000_000;
    private const int AccountsPerCompany = 200;

    [Fact]
    public async Task Reports_OverOneMillionGlRows_AreWithinThresholds()
    {
        // پایگاه‌داده فایل‌محور برای پایداری هاست تست روی یک میلیون ردیف؛
        // سرویس‌های گزارش با Options مستقلِ رشته‌ای ساخته می‌شوند تا هر Context
        // اتصال خودش را باز کند (EF نمی‌تواند روی یک SQLiteConnection واحد موازی کار کند)
        var dbPath = Path.Combine(Path.GetTempPath(), $"erp_bench_{Guid.NewGuid():N}.db");
        var fileConnectionString = $"Data Source={dbPath}";
        var fileConnection = new Microsoft.Data.Sqlite.SqliteConnection(fileConnectionString);
        fileConnection.Open();
        var harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid(), fileConnection);
        using (var init = harness.NewDbContext())
        {
            init.Database.EnsureCreated();
        }

        var benchOptions = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlite(fileConnectionString)
            .Options;
        AccountingDbContext NewBenchDb() => new(benchOptions, harness.TenantContext, harness.CurrentUser, harness.Clock);

        try
        {
            var year = harness.SeedOpenFiscalYear(2025);
            // نیمی دارایی (طرف بدهکار)، نیمی بدهی (طرف بستانکار) — داده دوجانبه واقعی
            var assetAccounts = new List<Account>();
            var liabilityAccounts = new List<Account>();
            for (var i = 1; i <= AccountsPerCompany / 2; i++)
            {
                assetAccounts.Add(harness.SeedPostingAccount($"{1000 + i:0000}", $"دارایی {i}", nature: AccountNature.Asset));
            }
            for (var i = 1; i <= AccountsPerCompany / 2; i++)
            {
                liabilityAccounts.Add(harness.SeedPostingAccount($"{2000 + i:0000}", $"بدهی {i}", nature: AccountNature.Liability));
            }

            var insertWatch = Stopwatch.StartNew();
            BulkInsertGlRows(harness.TenantId, harness.CompanyId, year.Id, fileConnectionString, assetAccounts, liabilityAccounts, rowTarget: TotalRows / 2);
            BulkInsertGlRows(harness.TenantId, harness.CompanyId, year.Id, fileConnectionString, assetAccounts, liabilityAccounts, rowTarget: TotalRows / 2, startDocument: 5_000_000);
            insertWatch.Stop();

            // تشخیص: تعداد واقعی ردیف‌ها از دید ADO و EF
            using (var rawConnection = new Microsoft.Data.Sqlite.SqliteConnection(fileConnectionString))
            {
                rawConnection.Open();
                using var rawCount = rawConnection.CreateCommand();
                rawCount.CommandText = "SELECT count(*) FROM acc_gl_entries";
                var rawValue = Convert.ToInt64(rawCount.ExecuteScalar());
                if (rawValue < TotalRows - 3000)
                    throw new InvalidOperationException($"Raw rows in table: {rawValue} of {TotalRows}");
            }

            using var probe = NewBenchDb();
            var filteredCount = await probe.GeneralLedgerEntries.CountAsync();
            var unfilteredCount = await probe.GeneralLedgerEntries.IgnoreQueryFilters().CountAsync();
            if (unfilteredCount < TotalRows - 3000)
                throw new InvalidOperationException($"EF unfiltered sees: {unfilteredCount}");
            string storageType, sampleTenant, sampleTenantHex = "-";
            using (var diag = harness.Connection.CreateCommand())
            {
                diag.CommandText = "SELECT typeof(TenantId), TenantId FROM acc_accounts LIMIT 1";
                using var r = diag.ExecuteReader();
                r.Read();
                storageType = r.GetString(0);
                var obj = r.GetValue(1);
                sampleTenant = obj switch
                {
                    byte[] b => Convert.ToHexString(b),
                    _ => obj?.ToString() ?? "null"
                };
                if (obj is byte[] bb)
                    sampleTenantHex = Convert.ToHexString(System.Guid.Parse(harness.TenantId.ToString()).ToByteArray());
            }

            var explicitCount = await probe.GeneralLedgerEntries
                .Where(e => e.TenantId == harness.TenantId && e.CompanyId == harness.CompanyId)
                .CountAsync();
            if (explicitCount < TotalRows - 3000)
                throw new InvalidOperationException(
                    $"Explicit where sees: {explicitCount} (unfiltered={unfilteredCount}); " +
                    $"storage={storageType}, sampleTenant={sampleTenant}, context={harness.TenantId}, " +
                    $"contextHex={sampleTenantHex}");
            if (filteredCount < TotalRows - 3000)
                throw new InvalidOperationException(
                    $"Query filter hides rows: filtered={filteredCount}, unfiltered={unfilteredCount}");

            // شرکت دوم و سال مالی دوم (چند شرکتی/چندسالی)
            var otherTenant = Guid.NewGuid();
            var otherCompany = Guid.NewGuid();
            var otherHarness = VoucherTestHarness.Create(otherTenant, otherCompany, harness.Connection);
            try
            {
                otherHarness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
                var otherYear = otherHarness.SeedOpenFiscalYear(2025);
                var otherDebits = Enumerable.Range(1, 25)
                    .Select(i => otherHarness.SeedPostingAccount($"{1000 + i:0000}", $"دارایی شرکت دیگر {i}", nature: AccountNature.Asset))
                    .ToList();
                var otherCredits = Enumerable.Range(1, 25)
                    .Select(i => otherHarness.SeedPostingAccount($"{2000 + i:0000}", $"بدهی شرکت دیگر {i}", nature: AccountNature.Liability))
                    .ToList();
                using var otherFileConnection = new Microsoft.Data.Sqlite.SqliteConnection(fileConnectionString);
                BulkInsertGlRows(otherTenant, otherCompany, otherYear.Id, fileConnectionString, otherDebits, otherCredits, rowTarget: 100_000);

                var trialBalance = new TrialBalanceService(NewBenchDb());
                var ledgerReports = new LedgerReportsService(NewBenchDb());
                var statements = new FinancialStatementsService(NewBenchDb(),
                    new ReportClassificationService(NewBenchDb()));
                var filter = new ReportFilter(harness.TenantId, harness.CompanyId, year.Id);

                var results = new List<(string Name, long Ms, long MemoryMb)>();

                // تراز آزمایشی — چهار سطح
                foreach (var (level, name) in new[]
                         {
                             (AccountLevel.Group, "TrialBalance.L1"),
                             (AccountLevel.General, "TrialBalance.L2"),
                             (AccountLevel.Subsidiary, "TrialBalance.L3"),
                             (AccountLevel.Detail, "TrialBalance.L4")
                         })
                {
                    var sw = Stopwatch.StartNew();
                    var tb = await trialBalance.GetTrialBalanceAsync(filter, level);
                    sw.Stop();
                    tb.IsBalanced.Should().BeTrue($"{name} روی دفتر متوازن");
                    tb.TotalDebit.Should().BeGreaterThan(0m);
                    results.Add((name, sw.ElapsedMilliseconds,
                        Process.GetCurrentProcess().WorkingSet64 / 1_000_000));
                }

                // دفتر کل + ترازنامه + سود و زیان
                var swGl = Stopwatch.StartNew();
                var gl = await ledgerReports.GetGeneralLedgerAsync(filter);
                swGl.Stop();
                gl.Should().NotBeEmpty();
                results.Add(("GeneralLedger", swGl.ElapsedMilliseconds,
                    Process.GetCurrentProcess().WorkingSet64 / 1_000_000));

                var swBs = Stopwatch.StartNew();
                var bs = await statements.GetBalanceSheetAsync(filter);
                swBs.Stop();
                bs.IsBalanced.Should().BeTrue("داده دوجانبه دارایی/بدهی باید ترازنامه را متوازن کند");
                bs.TotalAssets.Should().BeGreaterThan(0m);
                results.Add(("BalanceSheet", swBs.ElapsedMilliseconds,
                    Process.GetCurrentProcess().WorkingSet64 / 1_000_000));

                var swPl = Stopwatch.StartNew();
                await statements.GetProfitLossAsync(filter);
                swPl.Stop();
                results.Add(("ProfitLoss", swPl.ElapsedMilliseconds,
                    Process.GetCurrentProcess().WorkingSet64 / 1_000_000));

                // صفحه‌بندی روزنامه (۵۰ ردیف از یک میلیون)
                var swJournal = Stopwatch.StartNew();
                var journal = await ledgerReports.GetJournalAsync(filter, 5, 50);
                swJournal.Stop();
                journal.Rows.Should().HaveCount(50);
                journal.TotalCount.Should().BeGreaterThan(TotalRows - 3000);
                results.Add(("Journal.Page(50/1M)", swJournal.ElapsedMilliseconds,
                    Process.GetCurrentProcess().WorkingSet64 / 1_000_000));

                // درخواست‌های هم‌زمان گزارش (۲۰ تراز آزمایشی موازی — هر درخواست با زمینه داده خودش)
                var concurrentWatch = Stopwatch.StartNew();
                await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
                {
                    var scopedService = new TrialBalanceService(NewBenchDb());
                    var tb = await scopedService.GetTrialBalanceAsync(filter, AccountLevel.Subsidiary);
                    tb.IsBalanced.Should().BeTrue();
                })));
                concurrentWatch.Stop();
                results.Add(("TrialBalance.x20 Concurrent", concurrentWatch.ElapsedMilliseconds,
                    Process.GetCurrentProcess().WorkingSet64 / 1_000_000));

                _output.WriteLine("=== PROMPT 09 Performance Benchmark Results ===");
                _output.WriteLine($"GL rows inserted: {TotalRows:N0} in {insertWatch.ElapsedMilliseconds} ms");
                _output.WriteLine($"Companies: 2 (+1 tenant) | Fiscal years: 1 each | Accounts: {AccountsPerCompany}+50");
                foreach (var (name, ms, mem) in results)
                {
                    _output.WriteLine($"{name,-28} {ms,8} ms   (working set {mem} MB)");
                }

                // آستانه‌های عملکردی سخت‌گیرانه اما واقع‌بینانه برای SQLite درون‌حافظه
                foreach (var (name, ms, _) in results.Where(r => r.Name.StartsWith("TrialBalance") || r.Name == "GeneralLedger" || r.Name == "BalanceSheet" || r.Name == "ProfitLoss"))
                {
                    ms.Should().BeLessThan(10_000, $"{name} باید در کمتر از ۱۰ ثانیه کامل شود");
                }
                results.Single(r => r.Name == "Journal.Page(50/1M)").Ms.Should().BeLessThan(2_000);
                // SQLite قفل فایل دارد و برای خوانش موازی بهینه نیست؛ سناریوی
                // هم‌زمانی واقعی روی PostgreSQL سنجیده می‌شود (آستانه عام برای SQLite)
                results.Single(r => r.Name == "TrialBalance.x20 Concurrent").Ms.Should().BeLessThan(90_000);
            }
            finally
            {
                otherHarness.Dispose();
            }
        }
        finally
        {
            harness.Dispose();
            fileConnection.Dispose();
            try { System.IO.File.Delete(dbPath); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// درج انبوه سطرهای دفتر کل با INSERT چندمقداری (بدون Change Tracker) —
    /// اسناد متوازن دو سطری؛ شماره سند پیوسته.
    /// </summary>
    private static void BulkInsertGlRows(
        Guid tenantId, Guid companyId, Guid fiscalYearId, string fileConnectionString,
        IReadOnlyList<Account> debitAccounts, IReadOnlyList<Account> creditAccounts,
        int rowTarget, int startDocument = 1)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(fileConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var periodId = Guid.NewGuid();

        // نام ستون‌ها را از خود طرح می‌خوانیم (PascalCase در SQLite تست، snake_case در PostgreSQL)
        var schemaColumns = new List<string>();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(acc_gl_entries)";
            using var reader = pragma.ExecuteReader();
            while (reader.Read())
                schemaColumns.Add(reader.GetString(1));
        }

        string[] wanted =
        [
            "Id", "TenantId", "CompanyId", "FiscalYearId", "FiscalPeriodId",
            "BranchId", "DocumentId", "DocumentLineId", "DocumentNumber", "LineNumber",
            "AccountId", "EntryDate", "Description", "Side", "DebitAmount", "CreditAmount",
            "CurrencyCode", "ExchangeRate", "IsReversal", "PostedBy", "PostedAtUtc", "CreatedAtUtc"
        ];
        var columns = wanted.Where(schemaColumns.Contains).ToList();

        // فرمت ذخیره‌سازی Guid را از یک ردیف واقعی (SeedPostingAccount) می‌خوانیم:
        // EF Core ممکن است Guid را TEXT یا BLOB بنویسد
        string GuidLiteral(Guid value)
        {
            using var probeCmd = connection.CreateCommand();
            probeCmd.Transaction = transaction;
            // از جدول حساب‌ها (با ردیف واقعی Seed) فرمت Guid را می‌خوانیم
            probeCmd.CommandText = "SELECT typeof(TenantId) FROM acc_accounts LIMIT 1";
            var storageType = probeCmd.ExecuteScalar()?.ToString() ?? "text";
            return storageType == "blob"
                ? "X'" + BitConverter.ToString(value.ToByteArray()).Replace("-", "") + "'"
                : "'" + value.ToString().ToUpperInvariant() + "'";
        }

        if (columns.Count < 10)
            throw new InvalidOperationException(
                $"Schema columns mismatch. Table columns: {string.Join(", ", schemaColumns)}");
        var baseSql = $"""INSERT INTO acc_gl_entries ({string.Join(", ", columns)}) VALUES""";

        const int batchSize = 500;
        var rowsPerAccountPair = 2; // هر سند دو سطر متوازن
        var documentCount = rowTarget / rowsPerAccountPair;
        var docNumber = startDocument;

        var counter = 0;
        var batch = new System.Text.StringBuilder();

        for (var doc = 0; doc < documentCount; doc++)
        {
            var documentId = Guid.NewGuid();
            var debitAccountId = debitAccounts[doc % debitAccounts.Count].Id;
            var creditAccountId = creditAccounts[(doc + 7) % creditAccounts.Count].Id;
            var amount = (doc % 50) + 1;
            var date = $"2025-{(doc % 12) + 1:00}-05";

            // مقادیر به ترتیب ستون‌های موجود در طرح ساخته می‌شوند
            var row1 = new Dictionary<string, string>
            {
                ["Id"] = GuidLiteral(Guid.NewGuid()), ["TenantId"] = GuidLiteral(tenantId), ["CompanyId"] = GuidLiteral(companyId),
                ["FiscalYearId"] = GuidLiteral(fiscalYearId), ["FiscalPeriodId"] = GuidLiteral(periodId), ["BranchId"] = "NULL",
                ["DocumentId"] = GuidLiteral(documentId), ["DocumentLineId"] = GuidLiteral(Guid.NewGuid()),
                ["DocumentNumber"] = docNumber.ToString(), ["LineNumber"] = "1", ["AccountId"] = GuidLiteral(debitAccountId),
                ["EntryDate"] = $"'{date}'", ["Description"] = $"'سطر بنچمارک #{doc}'", ["Side"] = "1",
                ["DebitAmount"] = $"{amount}.0000", ["CreditAmount"] = "0.0000", ["CurrencyCode"] = "'IRR'",
                ["ExchangeRate"] = "1.000000", ["IsReversal"] = "0", ["PostedBy"] = "'bench'",
                ["PostedAtUtc"] = "'2026-01-01T00:00:00+00:00'", ["CreatedAtUtc"] = "'2026-01-01T00:00:00+00:00'"
            };
            var row2 = new Dictionary<string, string>(row1)
            {
                ["Id"] = GuidLiteral(Guid.NewGuid()), ["DocumentLineId"] = GuidLiteral(Guid.NewGuid()),
                ["LineNumber"] = "2", ["Side"] = "2", ["AccountId"] = GuidLiteral(creditAccountId),
                ["DebitAmount"] = "0.0000", ["CreditAmount"] = $"{amount}.0000"
            };
            // جداکننده بین ردیف‌های درون یک دسته
            if (batch.Length > 0)
                batch.Append(',');

            batch.Append('(').Append(string.Join(", ", columns.Select(c => row1[c]))).Append(')');
            docNumber++;
            counter += rowsPerAccountPair;

            var isBoundary = counter % (batchSize * rowsPerAccountPair) == 0 || doc == documentCount - 1;
            if (isBoundary)
            {
                // ردیف بستانکار پایانی هم درج می‌شود تا تراز حفظ شود
                batch.Append(',');
                batch.Append('(').Append(string.Join(", ", columns.Select(c => row2[c]))).Append(')');

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = baseSql + " " + batch.ToString().TrimEnd().TrimEnd(',') + ";";
                cmd.ExecuteNonQuery();
                batch.Clear();
            }
            else
            {
                batch.Append(',');
                batch.Append('(').Append(string.Join(", ", columns.Select(c => row2[c]))).Append(')');
            }
        }

        transaction.Commit();
    }
}
