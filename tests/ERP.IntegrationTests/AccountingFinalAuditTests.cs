using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Services.Reports;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ERP.IntegrationTests;

/// <summary>
/// PROMPT 10 — سناریوی کامل حسابداری End-to-End: یک شرکت آزمایشی واقع‌گرایانه
/// (Tenant/Company/Branch/دو سال مالی/کامل‌ترین کدینگ حساب‌ها/تفصیلی/مرکز هزینه)
/// و طی سناریوهای ۱ تا ۸ پر می‌شود. این Fixture یک‌بار اجرا و بین دو کلاس تست
/// (سناریوها و ناوردایی‌ها) مشترک است.
/// </summary>
public sealed class FinalAuditCompanyFixture : IDisposable
{
    public const decimal CapitalAmount = 500_000_000m;
    public const decimal SalesOnCredit = 120_000_000m;
    public const decimal CustomerReceipt = 120_000_000m;
    public const decimal CreditPurchase = 80_000_000m;
    public const decimal SupplierPayment = 50_000_000m;
    public const decimal WrongExpense = 10_000_000m;
    public const decimal CorrectedExpense = 12_000_000m;
    public const decimal DirectRevenue = 200_000_000m;

    public VoucherTestHarness Harness { get; } = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
    public Guid BranchId { get; } = Guid.NewGuid();
    public FiscalYear Year1 { get; private set; } = null!;
    public FiscalYear Year2 { get; private set; } = null!;
    public Account Bank { get; private set; } = null!;
    public Account Inventory { get; private set; } = null!;
    public Account CustomerLedger { get; private set; } = null!;
    public Account CustomerDetail { get; private set; } = null!;
    public Account Payables { get; private set; } = null!;
    public Account Capital { get; private set; } = null!;
    public Account RetainedEarnings { get; private set; } = null!;
    public Account SalesRevenue { get; private set; } = null!;
    public Account RentExpense { get; private set; } = null!;
    public Account SalaryExpense { get; private set; } = null!;
    public Guid CostCenterA { get; private set; }
    public Guid CostCenterB { get; private set; }
    public (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) Svc { get; private set; }

    public Dictionary<string, Guid> PostedDocuments { get; } = new();
    public Dictionary<string, string> ScenarioLog { get; } = new();

    public FinalAuditCompanyFixture()
    {
        SeedCompanyStructure();
        SeedFinancialDimensions();
        RunAllScenarios();
    }

    private void SeedCompanyStructure()
    {
        Harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        Year1 = Harness.SeedOpenFiscalYear(2025);
        Year2 = Harness.SeedOpenFiscalYear(2026);

        Bank = Harness.SeedPostingAccount("1010", "بانک", nature: AccountNature.Asset);
        Inventory = Harness.SeedPostingAccount("1201", "موجودی کالا", nature: AccountNature.Asset);
        var receivablesRoot = Harness.SeedPostingAccount("1101", "حساب دریافتنی مشتریان", nature: AccountNature.Asset);
        CustomerLedger = receivablesRoot;

        // حساب تفصیلی مشتری — فرزند معین دریافتنی
        using (var db = Harness.NewDbContext())
        {
            var tracked = db.Accounts.First(a => a.Id == receivablesRoot.Id);
            CustomerDetail = tracked.CreateChild("1", "مشتری الف", allowPosting: true);
            db.Accounts.Add(CustomerDetail);
            db.SaveChanges();
        }

        Payables = Harness.SeedPostingAccount("2101", "حساب پرداختنی تأمین‌کنندگان", nature: AccountNature.Liability);
        Capital = Harness.SeedPostingAccount("3101", "سرمایه", nature: AccountNature.Equity);
        RetainedEarnings = Harness.SeedPostingAccount("3102", "سود انباشته", nature: AccountNature.Equity);
        SalesRevenue = Harness.SeedPostingAccount("4101", "درآمد فروش", nature: AccountNature.Revenue);
        RentExpense = Harness.SeedPostingAccount("5101", "هزینه اجاره", nature: AccountNature.Expense);
        SalaryExpense = Harness.SeedPostingAccount("5102", "هزینه حقوق", nature: AccountNature.Expense);

        Svc = Harness.NewServices();
    }

    private void SeedFinancialDimensions()
    {
        using var db = Harness.NewDbContext();
        var dimensions = new FinancialDimensionService(db);
        CostCenterA = dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            Harness.TenantId, Harness.CompanyId, FinancialDimensionType.CostCenter,
            "CC-A", "مرکز هزینه فروش", "final-audit")).Result.Id;
        CostCenterB = dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            Harness.TenantId, Harness.CompanyId, FinancialDimensionType.CostCenter,
            "CC-B", "مرکز هزینه اداری", "final-audit")).Result.Id;
        dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            Harness.TenantId, Harness.CompanyId, FinancialDimensionType.DetailAccount,
            "C-101", "مشتری الف (شناور)", "final-audit")).Wait();
    }

    private async Task<Guid> PostAsync(string key, DateOnly date, string description,
        Guid debitAccount, Guid creditAccount, decimal amount, Guid? costCenterId = null, Guid? detailAccountId = null)
    {
        var draft = await Svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            Harness.TenantId, Harness.CompanyId, Year1.Id, date, description,
            [
                new VoucherLineCommand(debitAccount, DebitCredit.Debit, amount, description,
                    CostCenterId: costCenterId, DetailAccountId: detailAccountId),
                new VoucherLineCommand(creditAccount, DebitCredit.Credit, amount, description,
                    CostCenterId: costCenterId, DetailAccountId: detailAccountId)
            ],
            DocumentType.General, BranchId, null, null, null, null, "final-audit"));

        var posted = await Svc.Engine.PostAsync(new PostVoucherCommand(
            Harness.TenantId, Harness.CompanyId, draft.Id, null, "final-audit"));

        PostedDocuments[key] = draft.Id;
        return draft.Id;
    }

    private void RunAllScenarios()
    {
        // سناریو ۱: ثبت سرمایه اولیه — بدهکار بانک / بستانکار سرمایه
        PostAsync("S1", new DateOnly(2025, 1, 2), "سرمایه‌گذاری اولیه مالکان",
            Bank.Id, Capital.Id, CapitalAmount).GetAwaiter().GetResult();
        ScenarioLog["S1"] = $"سرمایه {CapitalAmount:N0} ثبت شد";

        // پیش‌نیاز واقع‌گرایانه سناریو ۲: فروش نسیه به مشتری (بدهکار دریافتنی/بستانکار درآمد)
        PostAsync("S2a", new DateOnly(2025, 1, 5), "فروش نسیه به مشتری الف",
            CustomerDetail.Id, SalesRevenue.Id, SalesOnCredit).GetAwaiter().GetResult();
        ScenarioLog["S2a"] = $"فروش نسیه {SalesOnCredit:N0} به دریافتنی مشتری";

        // سناریو ۲: دریافت وجه از مشتری — بدهکار بانک / بستانکار دریافتنی مشتری
        PostAsync("S2", new DateOnly(2025, 1, 20), "دریافت از مشتری الف",
            Bank.Id, CustomerDetail.Id, CustomerReceipt).GetAwaiter().GetResult();
        ScenarioLog["S2"] = $"وصول {CustomerReceipt:N0} از مشتری";

        // سناریو ۳: خرید نسیه — بدهکار موجودی کالا / بستانکار پرداختنی
        PostAsync("S3", new DateOnly(2025, 2, 3), "خرید نسیه کالا از تأمین‌کننده",
            Inventory.Id, Payables.Id, CreditPurchase).GetAwaiter().GetResult();
        ScenarioLog["S3"] = $"خرید نسیه {CreditPurchase:N0}";

        // سناریو ۴: پرداخت بدهی — بدهکار پرداختنی / بستانکار بانک
        PostAsync("S4", new DateOnly(2025, 2, 18), "پرداخت بخشی از بدهی تأمین‌کننده",
            Payables.Id, Bank.Id, SupplierPayment).GetAwaiter().GetResult();
        ScenarioLog["S4"] = $"پرداخت {SupplierPayment:N0} به تأمین‌کننده";

        // سناریو ۵: ثبت هزینه — بدهکار هزینه اجاره / بستانکار بانک (با مرکز هزینه)
        PostAsync("S5", new DateOnly(2025, 3, 1), "هزینه اجاره دفتر مرکزی",
            RentExpense.Id, Bank.Id, WrongExpense, costCenterId: CostCenterB).GetAwaiter().GetResult();
        ScenarioLog["S5"] = $"هزینه ثبت‌شده {WrongExpense:N0} (بعداً اشتباه تشخیص داده می‌شود)";

        // سناریو ۶: ثبت درآمد — بدهکار بانک / بستانکار درآمد فروش (مرکز هزینه فروش)
        PostAsync("S6", new DateOnly(2025, 3, 10), "درآمد فروش نقدی",
            Bank.Id, SalesRevenue.Id, DirectRevenue, costCenterId: CostCenterA).GetAwaiter().GetResult();
        ScenarioLog["S6"] = $"درآمد {DirectRevenue:N0}";

        // سناریو ۷: اصلاح سند قطعی اشتباه — معکوس‌سازی سند هزینه و ثبت سند صحیح
        var reversal = Svc.Engine.ReverseAsync(new ReverseVoucherCommand(
            Harness.TenantId, Harness.CompanyId, PostedDocuments["S5"],
            new DateOnly(2025, 3, 15), "اصلاح: هزینه ثبت‌شده اشتباه بود", null, "final-audit")
        ).GetAwaiter().GetResult();
        PostedDocuments["S7-reversal"] = reversal.ReversalDocumentId;

        PostAsync("S7-corrected", new DateOnly(2025, 3, 16), "اصلاحیه: هزینه حقوق صحیح",
            SalaryExpense.Id, Bank.Id, CorrectedExpense, costCenterId: CostCenterB).GetAwaiter().GetResult();
        ScenarioLog["S7"] = $"سند اشتباه معکوس شد؛ اصلاحیه {CorrectedExpense:N0} ثبت شد";

        // سناریو ۸: اختتامیه — بستن سال ۱ و انتقال مانده‌ها به افتتاحیه سال ۲
        var closing = new FiscalClosingWorkflowService(
            Harness.NewDbContext(), Svc.Docs, Svc.Engine)
            .RunClosingAsync(new RunFiscalClosingCommand(
                Harness.TenantId, Harness.CompanyId, Year1.Id, RetainedEarnings.Id, "final-audit")
            ).GetAwaiter().GetResult();
        ScenarioLog["S8-closing"] = $"اختتامیه انجام شد (سند بستن: {closing.ClosingDocumentId})";

        var opening = new OpeningVoucherService(
            Harness.NewDbContext(), Svc.Docs, Svc.Engine, new EmptyOpeningInventorySource())
            .CreateOpeningVoucherAsync(new OpeningVoucherCommand(
                Harness.TenantId, Harness.CompanyId, Year2.Id, "final-audit")
            ).GetAwaiter().GetResult();
        PostedDocuments["S8-opening"] = opening.DocumentId;
        ScenarioLog["S8-opening"] = $"افتتاحیه سال ۲: سند {opening.DocumentNumber}";
    }

    public void Dispose() => Harness.Dispose();
}

/// <summary>سناریوهای ۱ تا ۸ — نتیجه هر سناریو روی داده مشترک اعتبارسنجی می‌شود.</summary>
public class AccountingE2EScenarioTests(FinalAuditCompanyFixture fixture, ITestOutputHelper output) : IClassFixture<FinalAuditCompanyFixture>
{
    [Fact]
    public void AllEightScenarios_Completed_WithPostedBalancedDocuments()
    {
        using var db = fixture.Harness.NewDbContext();

        var requiredKeys = new[]
        {
            "S1", "S2a", "S2", "S3", "S4", "S5", "S6", "S7-corrected", "S8-opening"
        };
        requiredKeys.Where(k => !fixture.PostedDocuments.ContainsKey(k))
            .Should().BeEmpty("هر هشت سناریو باید سند قطعی داشته باشند");

        foreach (var (key, docId) in fixture.PostedDocuments)
        {
            var doc = db.Documents.Include(d => d.Lines).First(d => d.Id == docId);
            // سند S5 با معکوس‌سازی اصلاح شده؛ بقیه قطعی هستند
            new[] { DocumentStatus.Posted, DocumentStatus.Reversed }
                .Should().Contain(doc.Status, $"سند سناریوی {key}");
            doc.TotalDebit.Should().Be(doc.TotalCredit, $"تراز سند {key}");
            if (key == "S8-opening")
                doc.Lines.Should().HaveCountGreaterThan(1,
                    "سند افتتاحیه به تعداد حساب‌های دائمی با مانده ردیف دارد");
            else
                doc.Lines.Should().HaveCount(2, $"سندهای سناریوها دو ردیفی هستند ({key})");
        }

        foreach (var (key, log) in fixture.ScenarioLog)
            output.WriteLine($"[{key}] {log}");
    }

    [Fact]
    public void Scenario7_Reversal_NettedOriginalExpenseToZero()
    {
        using var db = fixture.Harness.NewDbContext();
        var rentNet = db.GeneralLedgerEntries
            .Where(e => e.AccountId == fixture.RentExpense.Id)
            .Sum(e => e.DebitAmount - e.CreditAmount);
        rentNet.Should().Be(0m, "اثر خالص سند اشتباه و معکوس آن صفر است");
    }
}

/// <summary>
/// سیزده ناوردایی مالی PROMPT 10 — اثبات با پرس‌وجو روی پایگاه‌داده نهایی سناریو.
/// </summary>
public class FinancialInvariantsTests(FinalAuditCompanyFixture fixture) : IClassFixture<FinalAuditCompanyFixture>
{
    private AccountingDbContext NewDb() => fixture.Harness.NewDbContext();

    // ۱ + ۲ + ۳ + ۴ + ۵ + ۷ — قیود ساختاری اسناد قطعی و دفتر کل
    [Fact]
    public async Task Invariants1to7_PostedDocumentsAndGlAreStructurallySound()
    {
        using var db = NewDb();

        var postedDocs = await db.Documents.Include(d => d.Lines)
            .Where(d => d.Status == DocumentStatus.Posted).ToListAsync();

        postedDocs.Should().NotBeEmpty();

        // ناوردایی ۱: جمع بدهکار و بستانکار هر سند قطعی برابر است
        postedDocs.Should().OnlyContain(d => d.TotalDebit == d.TotalCredit);

        // ناوردایی ۲: هیچ سند قطعی بدون دوره مالی معتبر نیست
        var periodIds = (await db.FiscalPeriods.ToListAsync()).Select(p => p.Id).ToHashSet();
        postedDocs.Should().OnlyContain(d => periodIds.Contains(d.FiscalPeriodId));

        // ناوردایی ۳: هیچ سند قطعی بدون حساب معتبر نیست
        var activeAccountIds = (await db.Accounts.Where(a => a.IsActive && a.AllowPosting)
            .ToListAsync()).Select(a => a.Id).ToHashSet();
        postedDocs.Should().OnlyContain(d => d.Lines.All(l => activeAccountIds.Contains(l.AccountId)));

        var glRows = await db.GeneralLedgerEntries.ToListAsync();
        // اسناد معکوس‌شده هم گردش دفتر کل دارند
        var postedOrReversedIds = (await db.Documents
            .Where(d => d.Status == DocumentStatus.Posted || d.Status == DocumentStatus.Reversed)
            .Select(d => d.Id).ToListAsync()).ToHashSet();
        var docIds = postedOrReversedIds;

        // ناوردایی ۴: هیچ گردش دفتر کل بدون سند معتبر وجود ندارد
        glRows.Should().OnlyContain(e => docIds.Contains(e.DocumentId));

        // ناوردایی ۵: هیچ سند قطعی بدون گردش متناظر وجود ندارد
        var glDocIds = glRows.Select(e => e.DocumentId).ToHashSet();
        postedDocs.Should().OnlyContain(d => glDocIds.Contains(d.Id));

        // ناوردایی ۷: هیچ سندی دوبار قطعی نمی‌شود — یکتایی (سند، ردیف) در دفتر کل
        glRows.GroupBy(e => new { e.DocumentId, e.DocumentLineId })
            .Should().OnlyContain(g => g.Count() == 1);
    }

    // ۶ — هیچ سند قطعی قابل ویرایش مستقیم نیست
    [Fact]
    public async Task Invariant6_NoPostedDocumentIsDirectlyEditable()
    {
        using var db = NewDb();
        var svc = fixture.Harness.NewServices().Docs;

        var postedDoc = await db.Documents
            .FirstAsync(d => d.Status == DocumentStatus.Posted);

        var act = () => svc.UpdateDraftAsync(new UpdateVoucherDraftCommand(
            fixture.Harness.TenantId, fixture.Harness.CompanyId, postedDoc.Id,
            postedDoc.DocumentDate, "تلاش دستکاری سند قطعی", [], null, "final-audit"));

        await act.Should().ThrowAsync<Exception>(
            "ویرایش سند قطعی باید با خطای دامنه رد شود");
    }

    // ۸ — هیچ Tenant به اطلاعات Tenant دیگر دسترسی ندارد
    [Fact]
    public async Task Invariant8_CrossTenantDataIsInvisible()
    {
        var otherTenant = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var otherHarness = VoucherTestHarness.Create(otherTenant, otherCompany, fixture.Harness.Connection);
        try
        {
            otherHarness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
            using var db = otherHarness.NewDbContext();

            (await db.Documents.CountAsync()).Should().Be(0, "Tenant دیگر هیچ سندی نمی‌بیند");
            (await db.GeneralLedgerEntries.CountAsync()).Should().Be(0, "Tenant دیگر هیچ گردشی نمی‌بیند");
            (await db.Accounts.CountAsync()).Should().Be(0, "Tenant دیگر هیچ حسابی نمی‌بیند");
        }
        finally
        {
            otherHarness.Dispose();
        }
    }

    // ۹ — مجموع مانده حساب‌ها با گردش دفتر کل تطابق دارد (مادی‌شده در برابر محاسبه‌شده)
    [Fact]
    public async Task Invariant9_SnapshotBalances_MatchGlTurnover()
    {
        using var db = NewDb();

        var glByAccount = await db.GeneralLedgerEntries
            .Where(e => e.FiscalYearId == fixture.Year1.Id)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(e => e.DebitAmount) - g.Sum(e => e.CreditAmount) })
            .ToListAsync();
        var glNetByAccount = glByAccount.ToDictionary(x => x.AccountId, x => x.Net);

        // Snapshotها دوره‌محورند — جمع همه دوره‌های سال = گردش تجمعی سال
        var snapshots = await db.AccountBalanceSnapshots
            .Where(s => s.FiscalYearId == fixture.Year1.Id)
            .GroupBy(s => s.AccountId)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(s => s.PeriodDebit) - g.Sum(s => s.PeriodCredit) })
            .ToListAsync();

        snapshots.Should().NotBeEmpty();
        foreach (var snap in snapshots)
        {
            var glNet = glNetByAccount.GetValueOrDefault(snap.AccountId, 0m);
            snap.Net.Should().Be(glNet,
                $"مانده مادی‌شده حساب {snap.AccountId} باید با گردش واقعی دفتر کل برابر باشد");
        }
    }

    // ۱۰ — تراز آزمایشی با اطلاعات دفتر کل برابر است
    [Fact]
    public async Task Invariant10_TrialBalanceMatchesGlTotals()
    {
        var trialBalance = new TrialBalanceService(NewDb());
        var filter = new ReportFilter(fixture.Harness.TenantId, fixture.Harness.CompanyId, fixture.Year1.Id);
        var tb = await trialBalance.GetTrialBalanceAsync(filter, AccountLevel.Detail);

        // تراز آزمایشی مانده‌محور است: مانده بدهکار/بستانکار هر حساب از گردش خالص GL ساخته می‌شود
        using var db = NewDb();
        var glNets = await db.GeneralLedgerEntries
            .Where(e => e.FiscalYearId == fixture.Year1.Id)
            .GroupBy(e => e.AccountId)
            .Select(g => g.Sum(e => e.DebitAmount) - g.Sum(e => e.CreditAmount))
            .ToListAsync();

        var glDebit = glNets.Sum(n => n > 0m ? n : 0m);
        var glCredit = glNets.Sum(n => n < 0m ? -n : 0m);

        tb.TotalDebit.Should().Be(glDebit, "جمع مانده بدهکار تراز آزمایشی = جمع مانده بدهکار از دفتر کل");
        tb.TotalCredit.Should().Be(glCredit, "جمع مانده بستانکار تراز آزمایشی = جمع مانده بستانکار از دفتر کل");
        tb.IsBalanced.Should().BeTrue();
    }

    // ۱۱ — ترازنامه از نظر معادله حسابداری متوازن است
    [Fact]
    public async Task Invariant11_BalanceSheetEquationHolds()
    {
        var statements = new FinancialStatementsService(NewDb(),
            new ReportClassificationService(NewDb()));
        var filter = new ReportFilter(fixture.Harness.TenantId, fixture.Harness.CompanyId, fixture.Year1.Id);
        var bs = await statements.GetBalanceSheetAsync(filter);

        // پس از اختتامیه، حساب‌های سود و زیان صفرند و سود در سود انباشته است
        bs.IsBalanced.Should().BeTrue("دارایی = بدهی + سرمایه (با سود انباشته اختتامیه)");
        bs.TotalAssets.Should().BeGreaterThan(0m);
    }

    // ۱۲ — افتتاحیه سال بعد با مانده‌های اختتامیه سازگار است
    [Fact]
    public async Task Invariant12_OpeningVoucherMatchesClosingBalances()
    {
        using var db = NewDb();

        // مانده نهایی حساب‌های دائمی در سال ۱ (پس از اختتامیه)
        var closingBalances = await db.GeneralLedgerEntries
            .Where(e => e.FiscalYearId == fixture.Year1.Id)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(e => e.DebitAmount) - g.Sum(e => e.CreditAmount) })
            .ToListAsync();
        var permanentBalances = closingBalances.Where(x => x.Net != 0).ToDictionary(x => x.AccountId, x => x.Net);

        permanentBalances.Should().NotBeEmpty("حساب‌های دائمی باید مانده داشته باشند");
        // حساب‌های موقت (درآمد/هزینه) باید صفر شده باشند
        closingBalances.Where(x => x.AccountId == fixture.SalesRevenue.Id
                                || x.AccountId == fixture.RentExpense.Id
                                || x.AccountId == fixture.SalaryExpense.Id)
            .Should().OnlyContain(x => x.Net == 0, "حساب‌های موقت با اختتامیه بسته می‌شوند");

        // گردش افتتاحیه سال ۲ باید دقیقاً همان مانده‌ها را بازسازی کند
        var openingDocId = fixture.PostedDocuments["S8-opening"];
        var openingRows = await db.GeneralLedgerEntries
            .Where(e => e.DocumentId == openingDocId)
            .GroupBy(e => e.AccountId)
            .Select(g => new { AccountId = g.Key, Net = g.Sum(e => e.DebitAmount) - g.Sum(e => e.CreditAmount) })
            .ToListAsync();
        var openingByAccount = openingRows.ToDictionary(x => x.AccountId, x => x.Net);

        openingByAccount.Keys.Should().BeEquivalentTo(permanentBalances.Keys,
            "افتتاحیه باید دقیقاً حساب‌های دائمی با مانده را پوشش دهد");
        foreach (var (accountId, closingNet) in permanentBalances)
            openingByAccount[accountId].Should().Be(closingNet,
                $"مانده افتتاحیه حساب {accountId} باید با اختتامیه برابر باشد");
    }

    // ۱۳ — گزارش‌ها فقط اطلاعات معتبر و قطعی را در بر می‌گیرند
    [Fact]
    public async Task Invariant13_ReportsContainOnlyPostedData()
    {
        using var db = NewDb();

        // هیچ سند Draft/Submitted نباید گردش دفتر کل داشته باشد
        var draftDocIds = await db.Documents
            .Where(d => d.Status != DocumentStatus.Posted && d.Status != DocumentStatus.Reversed)
            .Select(d => d.Id).ToListAsync();
        var glDocIds = (await db.GeneralLedgerEntries.Select(e => e.DocumentId).ToListAsync()).ToHashSet();
        draftDocIds.Should().NotIntersectWith(glDocIds,
            "اسناد غیرقطعی هیچ گردشی در دفتر کل ندارند");

        // تراز آزمایشی سال ۱ (برش تا پایان دوره) فقط اسناد قطعی همان سال را می‌بیند
        var trialBalance = new TrialBalanceService(NewDb());
        var filter = new ReportFilter(fixture.Harness.TenantId, fixture.Harness.CompanyId, fixture.Year1.Id);
        var tb = await trialBalance.GetTrialBalanceAsync(filter, AccountLevel.Detail);
        tb.Rows.Should().OnlyContain(r => r.PeriodDebit >= 0m && r.PeriodCredit >= 0m, "مقادیر گزارش نامنفی هستند");
    }
}
