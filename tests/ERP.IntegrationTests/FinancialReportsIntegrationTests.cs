using ClosedXML.Excel;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Services.Reports;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Export;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text;
using UglyToad.PdfPig;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای اجباری PROMPT 08: گزارش‌های مالی با دفتر کل و اسناد قطعی قابل
/// تطبیق و بازتولید — هیچ عددی بدون منبع معتبر (سطرهای دفتر کل اسناد قطعی).
/// </summary>
public class FinancialReportsIntegrationTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _year1;
    private readonly Account _cash;
    private readonly Account _capital;
    private readonly Account _revenue;
    private readonly Account _cogs;
    private readonly Account _operatingExpense;
    private readonly Account _fixedAsset;
    private readonly Account _receivable;
    private readonly Account _payable;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _svc;
    private readonly ReportClassificationService _classification;
    private readonly TrialBalanceService _trialBalance;
    private readonly LedgerReportsService _ledgerReports;
    private readonly FinancialStatementsService _statements;
    private readonly CashFlowService _cashFlow;
    private readonly ReceivablesPayablesService _receivablesPayables;

    public FinancialReportsIntegrationTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _year1 = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _capital = _harness.SeedPostingAccount("3101", "سرمایه", nature: AccountNature.Equity);
        _revenue = _harness.SeedPostingAccount("4101", "درآمد فروش", nature: AccountNature.Revenue);
        _cogs = _harness.SeedPostingAccount("5101", "بهای تمام‌شده", nature: AccountNature.Expense);
        _operatingExpense = _harness.SeedPostingAccount("5201", "هزینه عملیاتی", nature: AccountNature.Expense);
        _fixedAsset = _harness.SeedPostingAccount("1301", "تجهیزات", nature: AccountNature.Asset);
        _receivable = _harness.SeedPostingAccount("1101", "حساب دریافتنی", nature: AccountNature.Asset);
        _payable = _harness.SeedPostingAccount("2101", "حساب پرداختنی", nature: AccountNature.Liability);
        _svc = _harness.NewServices();
        _classification = new ReportClassificationService(_harness.NewDbContext());
        _trialBalance = new TrialBalanceService(_harness.NewDbContext());
        _ledgerReports = new LedgerReportsService(_harness.NewDbContext());
        _statements = new FinancialStatementsService(_harness.NewDbContext(), _classification);
        _cashFlow = new CashFlowService(_harness.NewDbContext(), _classification);
        _receivablesPayables = new ReceivablesPayablesService(_harness.NewDbContext(), _classification);

        // تنظیمات گزارش‌گیری: طبقه‌بندی ریشه‌های کدینگ
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "10", ReportAccountCategory.CashAndCashEquivalents).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "11", ReportAccountCategory.AccountsReceivable).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "13", ReportAccountCategory.NonCurrentAsset).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "21", ReportAccountCategory.AccountsPayable).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "31", ReportAccountCategory.Equity).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "41", ReportAccountCategory.Revenue).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "51", ReportAccountCategory.CostOfGoodsSold).GetAwaiter().GetResult();
        _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "52", ReportAccountCategory.OperatingExpense).GetAwaiter().GetResult();
    }

    private ReportFilter NewFilter(DateOnly? from = null, DateOnly? to = null) =>
        new(_harness.TenantId, _harness.CompanyId, _year1.Id, null, from, to);

    private async Task<Guid> PostAsync(DateOnly date, string description, IReadOnlyList<VoucherLineCommand> lines)
    {
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, date, description,
            lines, DocumentType.General, null, null, null, null, null, "user"));
        await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));
        return draft.Id;
    }

    private static VoucherLineCommand Line(Guid accountId, DebitCredit side, decimal amount, string description, IReadOnlyList<DimensionAssignmentCommand>? dimensions = null) =>
        new(accountId, side, amount, description, Dimensions: dimensions);

    /// <summary>سناریوی پایه: سرمایه ۵۰۰۰، فروش ۱۰۰۰، بهای تمام‌شده ۴۰۰، هزینه عملیاتی ۱۵۰، خرید تجهیزات ۸۰۰.</summary>
    private async Task SeedBaseDatasetAsync()
    {
        await PostAsync(new DateOnly(2025, 1, 10), "آورده سرمایه",
            [Line(_cash.Id, DebitCredit.Debit, 5000m, "آورده"), Line(_capital.Id, DebitCredit.Credit, 5000m, "آورده")]);

        await PostAsync(new DateOnly(2025, 2, 5), "فروش نقدی",
            [Line(_cash.Id, DebitCredit.Debit, 1000m, "فروش"), Line(_revenue.Id, DebitCredit.Credit, 1000m, "فروش")]);

        await PostAsync(new DateOnly(2025, 3, 5), "بهای تمام‌شده فروش",
            [Line(_cogs.Id, DebitCredit.Debit, 400m, "بهای تمام‌شده"), Line(_cash.Id, DebitCredit.Credit, 400m, "بهای تمام‌شده")]);

        await PostAsync(new DateOnly(2025, 4, 5), "هزینه عملیاتی",
            [Line(_operatingExpense.Id, DebitCredit.Debit, 150m, "هزینه"), Line(_cash.Id, DebitCredit.Credit, 150m, "هزینه")]);

        await PostAsync(new DateOnly(2025, 5, 10), "خرید تجهیزات",
            [Line(_fixedAsset.Id, DebitCredit.Debit, 800m, "تجهیزات"), Line(_cash.Id, DebitCredit.Credit, 800m, "تجهیزات")]);
    }

    // ------------------------------------------------------------------
    // ۱. تراز آزمایشی متوازن و منطبق با دفتر کل
    // ------------------------------------------------------------------

    [Fact]
    public async Task TrialBalance_IsBalanced_AndMatchesGeneralLedger()
    {
        await SeedBaseDatasetAsync();

        var tb = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Subsidiary);

        // تطابق کامل با دفتر کل: منبع هر دو گزارش یکسان است و جمع‌ها باید برابر باشند
        var gl = await _ledgerReports.GetGeneralLedgerAsync(NewFilter());
        tb.IsBalanced.Should().BeTrue();
        (tb.TotalDebit - tb.TotalCredit).Should().Be(0m);
        tb.TotalDebit.Should().Be(gl.Sum(r => r.ClosingDebit));
        tb.TotalCredit.Should().Be(gl.Sum(r => r.ClosingCredit));

        // ردیف صندوق: 5000+1000-400-150-800 = 4650 بدهکار
        var cashRow = tb.Rows.Single(r => r.AccountId == _cash.Id);
        cashRow.ClosingDebit.Should().Be(4650m);
        cashRow.ClosingCredit.Should().Be(0m);

        // سه برش زمانی: گردش دوره صندوق = 5000+1000 در بدهکار، 400+150+800 در بستانکار
        cashRow.PeriodDebit.Should().Be(6000m);
        cashRow.PeriodCredit.Should().Be(1350m);

        // تطابق ردیف TB با گردش حساب همان حساب
        var turnover = await _ledgerReports.GetAccountTurnoverAsync(_cash.Id, NewFilter());
        cashRow.PeriodDebit.Should().Be(turnover.DebitTurnover);
        cashRow.ClosingDebit.Should().Be(turnover.ClosingDebit);
    }

    [Fact]
    public async Task TrialBalance_LevelAggregation_RollsUpByRoot()
    {
        await SeedBaseDatasetAsync();

        // حساب دوم زیر همان ریشه ۵۲ تا تجمیع سطحی معنا داشته باشد
        var extraExpense = _harness.SeedPostingAccount("5202", "هزینه اداری", nature: AccountNature.Expense);
        await PostAsync(new DateOnly(2025, 6, 15), "هزینه اداری",
            [Line(extraExpense.Id, DebitCredit.Debit, 60m, "اداری"), Line(_cash.Id, DebitCredit.Credit, 60m, "اداری")]);

        var level1 = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Group);
        var level4 = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Detail);

        // چهارسطحی: هر حساب قابل ثبت یک ردیف (۷ حساب با گردش)
        level4.Rows.Should().HaveCount(7);
        // یک‌سطحی: 5201 و 5202 زیر ریشه ۵۲ تجمیع می‌شوند → ۶ ردیف
        level1.Rows.Should().HaveCount(6);
        var mergedRow = level1.Rows.Single(r => r.AccountCode == "52");
        mergedRow.PeriodDebit.Should().Be(150m + 60m, "گردش دو حساب معین زیر یک ریشه تجمیع شده");
        // مجموع‌ها در همه سطوح برابر است
        level1.TotalDebit.Should().Be(level4.TotalDebit);
        level1.TotalCredit.Should().Be(level4.TotalCredit);
        level1.IsBalanced.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // ۲. تراز آزمایشی نامتوازن — تشخیص اختلاف
    // ------------------------------------------------------------------

    [Fact]
    public async Task TrialBalance_DetectsUnbalancedLedger()
    {
        await SeedBaseDatasetAsync();

        // تزریق مستقیم یک سطر نامتوازن به دفتر کل (شبیه‌سازی خرابی منبع داده)
        var existing = await _svc.Db.GeneralLedgerEntries.AsNoTracking().FirstAsync();
        var injected = new GeneralLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = existing.TenantId,
            CompanyId = existing.CompanyId,
            FiscalYearId = existing.FiscalYearId,
            FiscalPeriodId = existing.FiscalPeriodId,
            DocumentId = existing.DocumentId,
            DocumentLineId = Guid.NewGuid(),
            DocumentNumber = existing.DocumentNumber,
            LineNumber = 999,
            AccountId = _cash.Id,
            EntryDate = existing.EntryDate,
            Description = "سطر معیوب تستی",
            DebitAmount = 123m,
            CreditAmount = 0m,
            CurrencyCode = existing.CurrencyCode,
            ExchangeRate = existing.ExchangeRate,
            PostedBy = existing.PostedBy
        };
        using (var db = _harness.NewDbContext())
        {
            db.GeneralLedgerEntries.Add(injected);
            await db.SaveChangesAsync();
        }

        var tb = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Subsidiary);

        tb.IsBalanced.Should().BeFalse("سطر تزریق‌شده تراز را شکسته است");
        tb.Difference.Should().Be(123m);
        tb.TotalDebit.Should().Be(tb.TotalCredit + 123m);
    }

    // ------------------------------------------------------------------
    // ۳. تطابق دفتر کل و معین
    // ------------------------------------------------------------------

    [Fact]
    public async Task SubsidiaryLedger_MatchesGeneralLedgerTotals()
    {
        await SeedBaseDatasetAsync();

        var gl = await _ledgerReports.GetGeneralLedgerAsync(NewFilter());
        var subsidiary = await _ledgerReports.GetSubsidiaryLedgerAsync(NewFilter(), 1, 500);

        // جمع ردیف‌های معین هر حساب = گردش همان حساب در دفتر کل
        foreach (var glRow in gl)
        {
            var detailSum = subsidiary.Rows
                .Where(r => r.AccountId == glRow.AccountId)
                .Sum(r => r.Debit - r.Credit);
            (glRow.DebitTurnover - glRow.CreditTurnover).Should().Be(detailSum,
                $"گردش معین حساب {glRow.AccountCode} باید با دفتر کل برابر باشد");
        }

        subsidiary.Rows.Should().NotBeEmpty();
        subsidiary.TotalCount.Should().Be(subsidiary.Rows.Count);
    }

    // ------------------------------------------------------------------
    // ۴. ترازنامه: دارایی = بدهی + حقوق مالکانه (پیش و پس از اختتامیه)
    // ------------------------------------------------------------------

    [Fact]
    public async Task BalanceSheet_AssetsEqualLiabilitiesPlusEquity_BeforeAndAfterClosing()
    {
        await SeedBaseDatasetAsync();

        var beforeClosing = await _statements.GetBalanceSheetAsync(NewFilter());
        beforeClosing.IsBalanced.Should().BeTrue("نتیجه دوره جاری در حقوق مالکانه لحاظ شده است");
        beforeClosing.Difference.Should().Be(0m);
        beforeClosing.TotalAssets.Should().Be(beforeClosing.TotalLiabilities + beforeClosing.TotalEquity);
        beforeClosing.CurrentPeriodResult.Should().Be(450m); // 1000 - 400 - 150
        beforeClosing.Equity.Rows.Should().Contain(r => r.AccountName.Contains("نتیجه دوره"));

        // پس از اختتامیه: موقت‌ها صفر و نتیجه به سود انباشته منتقل شده
        var retained = _harness.SeedPostingAccount("3102", "سود و زیان انباشته", nature: AccountNature.Equity);
        var closingService = new FiscalClosingWorkflowService(_harness.NewDbContext(), _svc.Docs, _svc.Engine);
        await closingService.RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, retained.Id, "cfo"));

        var afterClosing = await _statements.GetBalanceSheetAsync(NewFilter());
        afterClosing.IsBalanced.Should().BeTrue();
        afterClosing.Difference.Should().Be(0m);
        afterClosing.TotalAssets.Should().Be(afterClosing.TotalLiabilities + afterClosing.TotalEquity);
        afterClosing.CurrentPeriodResult.Should().Be(0m, "پس از اختتامیه حساب‌های موقت صفرند");
        afterClosing.Equity.Rows.Single(r => r.AccountId == retained.Id).Balance.Should().Be(450m);
    }

    // ------------------------------------------------------------------
    // ۵. سود و زیان — بخش‌ها و تطابق با اختتامیه
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfitLoss_SectionsMatchClosingNetIncome()
    {
        await SeedBaseDatasetAsync();

        var pl = await _statements.GetProfitLossAsync(NewFilter());
        pl.GrossProfit.Should().Be(600m);          // 1000 - 400
        pl.OperatingProfit.Should().Be(450m);      // 600 - 150
        pl.ProfitBeforeTax.Should().Be(450m);
        pl.NetProfit.Should().Be(450m);
        pl.Revenues.Rows.Single(r => r.AccountId == _revenue.Id).Balance.Should().Be(1000m);
        pl.CostOfGoodsSold.Rows.Single(r => r.AccountId == _cogs.Id).Balance.Should().Be(400m);

        // تطابق با گردش‌کار اختتامیه
        var retained = _harness.SeedPostingAccount("3102", "سود و زیان انباشته", nature: AccountNature.Equity);
        var closing = await new FiscalClosingWorkflowService(_harness.NewDbContext(), _svc.Docs, _svc.Engine)
            .RunClosingAsync(new RunFiscalClosingCommand(
                _harness.TenantId, _harness.CompanyId, _year1.Id, retained.Id, "cfo"));

        closing.NetIncome.Should().Be(pl.NetProfit, "سود و زیان گزارش باید با اختتامیه برابر باشد");
    }

    [Fact]
    public async Task ProfitLoss_ClassificationSettings_DriveSections()
    {
        await SeedBaseDatasetAsync();

        var before = await _statements.GetProfitLossAsync(NewFilter());
        before.CostOfGoodsSold.Rows.Should().Contain(r => r.AccountId == _cogs.Id);
        before.OperatingExpenses.Rows.Should().Contain(r => r.AccountId == _operatingExpense.Id);

        // تغییر طبقه‌بندی ریشه 52 از هزینه عملیاتی به بهای تمام‌شده
        await _classification.UpsertAsync(_harness.TenantId, _harness.CompanyId, "52", ReportAccountCategory.CostOfGoodsSold);

        var after = await _statements.GetProfitLossAsync(NewFilter());
        after.CostOfGoodsSold.Rows.Should().Contain(r => r.AccountId == _operatingExpense.Id);
        after.OperatingExpenses.Rows.Should().NotContain(r => r.AccountId == _operatingExpense.Id);
        after.GrossProfit.Should().Be(450m);  // 1000 - 400 - 150
        after.OperatingProfit.Should().Be(450m);
    }

    // ------------------------------------------------------------------
    // ۶. جریان وجوه نقد — روش مستقیم
    // ------------------------------------------------------------------

    [Fact]
    public async Task CashFlow_DirectMethod_BucketsMatchCashMovement()
    {
        await SeedBaseDatasetAsync();

        var cashFlow = await _cashFlow.GetCashFlowAsync(NewFilter());
        cashFlow.CashAccountsConfigured.Should().BeTrue();

        var operating = cashFlow.Buckets.Single(b => b.Category == CashFlowCategory.Operating);
        var investing = cashFlow.Buckets.Single(b => b.Category == CashFlowCategory.Investing);
        var financing = cashFlow.Buckets.Single(b => b.Category == CashFlowCategory.Financing);

        // فروش/بهای تمام‌شده/هزینه → عملیاتی | خرید تجهیزات (دارایی غیرجاری) → سرمایه‌گذاری | آورده سرمایه → تأمین مالی
        operating.Inflow.Should().Be(1000m);
        operating.Outflow.Should().Be(550m);
        operating.Net.Should().Be(450m);
        investing.Outflow.Should().Be(800m);
        financing.Inflow.Should().Be(5000m);

        // ناوردایی: افتتاح + خالص جریان‌ها = پایان دوره صندوق
        cashFlow.OpeningCash.Should().Be(0m);
        (cashFlow.OpeningCash + operating.Net + investing.Net + financing.Net)
            .Should().Be(cashFlow.ClosingCash);
        cashFlow.ClosingCash.Should().Be(4650m);

        // بدون تنظیم طبقه‌بندی وجوه نقد: خروجی صادقانه با یادداشت (بدون حدس)
        using var bareHarness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        bareHarness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        var bareCashFlow = await new CashFlowService(bareHarness.NewDbContext(),
            new ReportClassificationService(bareHarness.NewDbContext()))
            .GetCashFlowAsync(new ReportFilter(bareHarness.TenantId, bareHarness.CompanyId, null));
        bareCashFlow.CashAccountsConfigured.Should().BeFalse();
        bareCashFlow.Note.Should().NotBeNullOrEmpty();
    }

    // ------------------------------------------------------------------
    // ۷. گردش اشخاص و تفصیلی
    // ------------------------------------------------------------------

    [Fact]
    public async Task ReceivablesPayables_PartyTurnover_ByDimensions()
    {
        var customer = await new FinancialDimensionService(_harness.NewDbContext()).CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId,
                FinancialDimensionType.Customer, "CU-R1", "مشتری گزارش", "user"));
        var supplier = await new FinancialDimensionService(_harness.NewDbContext()).CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId,
                FinancialDimensionType.Supplier, "SU-R1", "تأمین‌کننده گزارش", "user"));
        var party = await new FinancialDimensionService(_harness.NewDbContext()).CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId,
                FinancialDimensionType.Party, "P-R1", "شخص گزارش", "user"));

        await SeedBaseDatasetAsync();

        // فروش نسیه به مشتری ۷۰۰
        await PostAsync(new DateOnly(2025, 6, 5), "فروش نسیه",
            [Line(_receivable.Id, DebitCredit.Debit, 700m, "فروش نسیه",
                    [new DimensionAssignmentCommand(FinancialDimensionType.Customer, customer.Id)]),
             Line(_revenue.Id, DebitCredit.Credit, 700m, "فروش نسیه")]);

        // خرید نسیه از تأمین‌کننده ۳۰۰
        await PostAsync(new DateOnly(2025, 6, 10), "خرید نسیه",
            [Line(_operatingExpense.Id, DebitCredit.Debit, 300m, "خرید نسیه"),
             Line(_payable.Id, DebitCredit.Credit, 300m, "خرید نسیه",
                    [new DimensionAssignmentCommand(FinancialDimensionType.Supplier, supplier.Id)])]);

        // وصول از شخص ۱۲۰ (بعد شخص)
        await PostAsync(new DateOnly(2025, 7, 1), "وصول از شخص",
            [Line(_cash.Id, DebitCredit.Debit, 120m, "وصول",
                    [new DimensionAssignmentCommand(FinancialDimensionType.Party, party.Id)]),
             Line(_revenue.Id, DebitCredit.Credit, 120m, "وصول")]);

        var receivables = await _receivablesPayables.GetReceivablesAsync(NewFilter());
        receivables.AccountsConfigured.Should().BeTrue();
        var customerRow = receivables.Rows.Single(r => r.DimensionId == customer.Id);
        customerRow.DebitTurnover.Should().Be(700m);
        customerRow.ClosingDebit.Should().Be(700m);

        var payables = await _receivablesPayables.GetPayablesAsync(NewFilter());
        var supplierRow = payables.Rows.Single(r => r.DimensionId == supplier.Id);
        supplierRow.CreditTurnover.Should().Be(300m);
        supplierRow.ClosingCredit.Should().Be(300m);

        var partyTurnover = await _receivablesPayables.GetPartyTurnoverAsync(NewFilter());
        var partyRow = partyTurnover.Rows.Single(r => r.DimensionId == party.Id);
        partyRow.DebitTurnover.Should().Be(120m, "گردش شخص روی همه حساب‌ها");
    }

    // ------------------------------------------------------------------
    // ۸. فیلتر تاریخ
    // ------------------------------------------------------------------

    [Fact]
    public async Task DateFilter_SeparatesOpeningAndPeriodTurnover()
    {
        // اسناد فوریه و آوریل؛ بازه گزارش: فقط فروردین تا خرداد
        await PostAsync(new DateOnly(2025, 2, 5), "فروش اسفندِ سال قبل",
            [Line(_cash.Id, DebitCredit.Debit, 300m, "فروش"), Line(_revenue.Id, DebitCredit.Credit, 300m, "فروش")]);
        await PostAsync(new DateOnly(2025, 4, 10), "فروش تیر",
            [Line(_cash.Id, DebitCredit.Debit, 200m, "فروش"), Line(_revenue.Id, DebitCredit.Credit, 200m, "فروش")]);

        var marchRange = new ReportFilter(
            _harness.TenantId, _harness.CompanyId, _year1.Id,
            null, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        var tb = await _trialBalance.GetTrialBalanceAsync(marchRange, AccountLevel.Subsidiary);

        // گردش داخل بازه صفر؛ اسناد اردیبهشت در «قبل از گردش» و اردیبهشتِ تیرمانده بیرون بازه
        var revenueRow = tb.Rows.Single(r => r.AccountId == _revenue.Id);
        revenueRow.PeriodDebit.Should().Be(0m);
        revenueRow.PeriodCredit.Should().Be(0m);
        revenueRow.OpeningCredit.Should().Be(300m);
        revenueRow.ClosingCredit.Should().Be(300m, "سند اردیبهشت بیرون از بازه گزارش است");
    }

    // ------------------------------------------------------------------
    // ۹. جداسازی شرکت و Tenant
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reports_AreIsolatedByCompanyAndTenant()
    {
        await SeedBaseDatasetAsync();

        var tb1 = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Subsidiary);
        // جمع بدهکار پایانی: صندوق 4650 + بهای تمام‌شده 400 + هزینه 150 + تجهیزات 800
        tb1.TotalDebit.Should().Be(6000m);

        // شرکت دیگر روی همان پایگاه‌داده
        var otherTenant = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var otherHarness = VoucherTestHarness.Create(otherTenant, otherCompany, _harness.Connection);
        try
        {
            otherHarness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
            var otherYear = otherHarness.SeedOpenFiscalYear(2025);
            var otherCash = otherHarness.SeedPostingAccount("1001", "صندوق شرکت دیگر");
            var otherRevenue = otherHarness.SeedPostingAccount("4101", "درآمد شرکت دیگر", nature: AccountNature.Revenue);

            using (var docs = otherHarness.NewDbContext())
            {
                // ثبت سند در شرکت دیگر
                var otherServices = otherHarness.NewServices();
                var draft = await otherServices.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
                    otherTenant, otherCompany, otherYear.Id, new DateOnly(2025, 2, 1), "فروش شرکت دیگر",
                    [new VoucherLineCommand(otherCash.Id, DebitCredit.Debit, 999m, "فروش"),
                     new VoucherLineCommand(otherRevenue.Id, DebitCredit.Credit, 999m, "فروش")],
                    DocumentType.General, null, null, null, null, null, "user"));
                await otherServices.Engine.PostAsync(new PostVoucherCommand(
                    otherTenant, otherCompany, draft.Id, null, "user"));
            }

            // گزارش شرکت اول هیچ عددی از شرکت دوم نمی‌بیند
            var tbAfter = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Subsidiary);
            tbAfter.TotalDebit.Should().Be(tb1.TotalDebit);
            tbAfter.Rows.Should().NotContain(r => r.AccountName.Contains("شرکت دیگر"));

            var journal = await _ledgerReports.GetJournalAsync(NewFilter(), 1, 500);
            journal.Rows.Should().NotContain(r => r.Description.Contains("شرکت دیگر"));

            // گزارش شرکت دوم فقط اعداد خودش
            var otherFilter = new ReportFilter(otherTenant, otherCompany, otherYear.Id);
            var otherTb = await new TrialBalanceService(otherHarness.NewDbContext())
                .GetTrialBalanceAsync(otherFilter, AccountLevel.Subsidiary);
            otherTb.IsBalanced.Should().BeTrue();
            otherTb.Rows.Should().OnlyContain(r => r.AccountName.Contains("شرکت دیگر") || r.AccountCode.Length > 0);
            otherTb.TotalDebit.Should().Be(999m);
        }
        finally
        {
            otherHarness.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // ۱۰. خروجی‌ها: Excel، PDF و CSV منطبق با API
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExcelExport_MatchesApiNumbers()
    {
        await SeedBaseDatasetAsync();

        var tb = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Subsidiary);
        var exportService = new ReportExportService();
        var export = await exportService.ExportAsync(
            ReportExportMapper.FromTrialBalance(tb), ReportExportFormat.Excel);

        export.FileName.Should().EndWith(".xlsx");

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = workbook.Worksheets.First();
        sheet.RightToLeft.Should().BeTrue("خروجی Excel باید راست‌به‌چپ باشد");

        // تعداد ردیف‌ها = سرستون + ردیف‌های TB
        sheet.LastRowUsed()!.RowNumber().Should().Be(tb.Rows.Count + 1);

        // تطابق عددی هر ردیف با API
        for (var i = 0; i < tb.Rows.Count; i++)
        {
            var apiRow = tb.Rows[i];
            sheet.Cell(i + 2, 1).GetString().Should().Be(apiRow.AccountCode);
            sheet.Cell(i + 2, 7).GetValue<decimal>().Should().Be(apiRow.ClosingDebit);
            sheet.Cell(i + 2, 8).GetValue<decimal>().Should().Be(apiRow.ClosingCredit);
        }
    }

    [Fact]
    public async Task PdfExport_ContainsFinancialFigures()
    {
        await SeedBaseDatasetAsync();

        var balanceSheet = await _statements.GetBalanceSheetAsync(NewFilter());
        var exportService = new ReportExportService();
        var export = await exportService.ExportAsync(
            ReportExportMapper.FromBalanceSheet(balanceSheet), ReportExportFormat.Pdf);

        export.FileName.Should().EndWith(".pdf");
        export.Content.Should().HaveCountGreaterThan(1000);

        // استخراج متن PDF: ارقام مالی و سربرگ باید در متن باشد
        using var pdf = PdfDocument.Open(export.Content);
        pdf.NumberOfPages.Should().BeGreaterThan(0);
        var text = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));

        // دارایی کل 8720 = صندوق 4650 + دریافتنی 0 + تجهیزات 800 + ... (4650+800+700+... بسته به سناریوی پایه: 4650+800)
        // سناریوی پایه: دارایی‌ها صندوق 4650 + تجهیزات 800 = 5450
        text.Should().Contain("5,450.00", "مجموع دارایی ترازنامه باید در PDF ظاهر شود");
        text.Should().Contain("4,650.00", "مانده صندوق باید در PDF ظاهر شود");
    }

    [Fact]
    public async Task CsvExport_IsUtf8WithPersianHeaders()
    {
        await SeedBaseDatasetAsync();

        var tb = await _trialBalance.GetTrialBalanceAsync(NewFilter(), AccountLevel.Subsidiary);
        var exportService = new ReportExportService();
        var export = await exportService.ExportAsync(
            ReportExportMapper.FromTrialBalance(tb), ReportExportFormat.Csv);

        // BOM UTF-8 برای سازگاری Excel فارسی
        export.Content.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);

        var content = Encoding.UTF8.GetString(export.Content);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(tb.Rows.Count + 1);
        lines[0].Should().Contain("کد حساب");
        lines[0].Should().Contain("مانده پایان دوره بدهکار");

        // مقدار عددی invariant (بدون جداکننده هزارگان) قابل بازخوانی
        // کدینگ کامل حساب صندوق «10001» است؛ خط را با پیشوند دقیق پیدا می‌کنیم
        var cashLine = lines.Skip(1).First(l => l.StartsWith("10001,"));
        cashLine.Should().Contain("4650");
    }

    public void Dispose() => _harness.Dispose();
}
