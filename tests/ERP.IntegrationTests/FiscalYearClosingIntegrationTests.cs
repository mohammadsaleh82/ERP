using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای اجباری PROMPT 07: افتتاحیه، اصلاحی، معکوس، اختتامیه و بازگشایی.
/// معیار پایان: چرخه مالی یک سال کامل از افتتاحیه تا اختتامیه و افتتاحیه سال بعد با حفظ تراز.
/// </summary>
public class FiscalYearClosingIntegrationTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _year1;
    private readonly FiscalYear _year2;
    private readonly Account _cash;
    private readonly Account _bank;
    private readonly Account _payable;
    private readonly Account _capital;
    private readonly Account _retainedEarnings;
    private readonly Account _revenue;
    private readonly Account _expense;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _svc;

    public FiscalYearClosingIntegrationTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _year1 = _harness.SeedOpenFiscalYear(2025);
        _year2 = _harness.SeedOpenFiscalYear(2026);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _bank = _harness.SeedPostingAccount("1002", "بانک");
        _payable = _harness.SeedPostingAccount("2101", "حساب پرداختنی", nature: AccountNature.Liability);
        _capital = _harness.SeedPostingAccount("3101", "سرمایه", nature: AccountNature.Equity);
        _retainedEarnings = _harness.SeedPostingAccount("3102", "سود و زیان انباشته", nature: AccountNature.Equity);
        _revenue = _harness.SeedPostingAccount("4101", "درآمد فروش", nature: AccountNature.Revenue);
        _expense = _harness.SeedPostingAccount("5101", "هزینه عملیاتی", nature: AccountNature.Expense);
        _svc = _harness.NewServices();
    }

    private FiscalClosingWorkflowService NewClosingService() =>
        new(_harness.NewDbContext(), _svc.Docs, _svc.Engine);

    private FiscalYearReopeningService NewReopeningService() =>
        new(_harness.NewDbContext());

    private OpeningVoucherService NewOpeningService(IOpeningInventorySource? inventorySource = null) =>
        new(_harness.NewDbContext(), _svc.Docs, _svc.Engine, inventorySource ?? new EmptyOpeningInventorySource());

    private AdjustmentVoucherService NewAdjustmentService() =>
        new(_harness.NewDbContext(), _svc.Docs, _svc.Engine);

    private async Task<Guid> PostYear1Doc(decimal cashDebit, decimal revenueCredit, DateOnly date, Guid? costCenterId = null)
    {
        var debitLine = new VoucherLineCommand(_cash.Id, DebitCredit.Debit, cashDebit, "فروش نقدی");
        var creditLine = new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, revenueCredit, "فروش نقدی", Dimensions: costCenterId.HasValue
            ? [new DimensionAssignmentCommand(FinancialDimensionType.CostCenter, costCenterId.Value)]
            : null);

        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, date, "سند فروش",
            [debitLine, creditLine], DocumentType.General, null, null, null, null, null, "user"));

        var posted = await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));

        return draft.Id;
    }

    private async Task<FiscalYearStatus> GetFiscalYearStatusAsync(Guid fiscalYearId)
    {
        using var db = _harness.NewDbContext();
        return (await db.FiscalYears.AsNoTracking().SingleAsync(y => y.Id == fiscalYearId)).Status;
    }

    /// <summary>مانده علامت‌دار یک حساب در یک سال مالی از دفتر کل (بدهکار مثبت).</summary>
    private async Task<decimal> GetSignedBalanceAsync(Guid accountId, Guid fiscalYearId)
    {
        using var db = _harness.NewDbContext();
        var rows = await db.GeneralLedgerEntries
            .Where(e => e.TenantId == _harness.TenantId && e.CompanyId == _harness.CompanyId
                        && e.FiscalYearId == fiscalYearId && e.AccountId == accountId)
            .Select(e => new { e.DebitAmount, e.CreditAmount })
            .ToListAsync();
        return rows.Sum(r => r.DebitAmount - r.CreditAmount);
    }

    // ------------------------------------------------------------------
    // افتتاحیه متوازن + انتقال مانده دائمی + عدم انتقال موقت
    // ------------------------------------------------------------------

    [Fact]
    public async Task FullCycle_Opening_ShouldCarryPermanentBalancesOnly()
    {
        // سال ۱: آورده سرمایه (نقد بدهکار/سرمایه بستانکار ۵۰۰۰)، خرید نسیه ۲۰۰،
        // فروش نقد ۳۰۰، هزینه ۸۰
        await PostPair(_cash, _capital, 5000m, new DateOnly(2025, 1, 5));
        await PostPair(_cash, _payable, 200m, new DateOnly(2025, 2, 5));
        await PostYear1Doc(300m, 300m, new DateOnly(2025, 3, 5));
        await PostPair(_expense, _cash, 80m, new DateOnly(2025, 4, 5));

        var closing = await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        closing.Status.Should().Be(FiscalClosingStatus.Closed);
        closing.NetIncome.Should().Be(220m); // ۳۰۰ - ۸۰

        var opening = await NewOpeningService().CreateOpeningVoucherAsync(new OpeningVoucherCommand(
            _harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        using (new AssertionScope())
        {
            // تراز افتتاحیه
            opening.TotalDebit.Should().Be(opening.TotalCredit, "سند افتتاحیه باید متوازن باشد");

            // انتقال مانده دائمی با علامت درست: صندوق ۵۴۲۰ بدهکار، پرداختنی ۲۰۰ بستانکار،
            // سرمایه ۵۰۰۰ بستانکار
            (await GetSignedBalanceAsync(_cash.Id, _year2.Id)).Should().Be(5420m);
            (await GetSignedBalanceAsync(_payable.Id, _year2.Id)).Should().Be(-200m);
            (await GetSignedBalanceAsync(_capital.Id, _year2.Id)).Should().Be(-5000m);

            // سود انباشته پس از اختتامیه به افتتاحیه منتقل شده (بستانکار → منفی)
            (await GetSignedBalanceAsync(_retainedEarnings.Id, _year2.Id)).Should().Be(-220m);

            // حساب موقت هرگز به افتتاحیه منتقل نمی‌شود
            (await GetSignedBalanceAsync(_revenue.Id, _year2.Id)).Should().Be(0m);
            (await GetSignedBalanceAsync(_expense.Id, _year2.Id)).Should().Be(0m);
        }
    }

    private async Task<PostDocumentResultDto> PostPair(Account debitAccount, Account creditAccount, decimal amount, DateOnly date)
    {
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, date, "سند آزمون",
            [
                new VoucherLineCommand(debitAccount.Id, DebitCredit.Debit, amount, "طرف بدهکار"),
                new VoucherLineCommand(creditAccount.Id, DebitCredit.Credit, amount, "طرف بستانکار")
            ],
            DocumentType.General, null, null, null, null, null, "user"));

        return await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));
    }

    // ------------------------------------------------------------------
    // افتتاحیه نامتوازن (قرارداد انبار با مانده نابالانس)
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnbalancedOpening_FromUnbalancedInventorySource_ShouldReject()
    {
        // آماده‌سازی سال قبل بسته با تراز طبیعی
        await PostPair(_capital, _cash, 1000m, new DateOnly(2025, 1, 5));
        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        var unbalancedSource = new FakeInventorySource([
            new OpeningInventoryLine(_cash.Id, 500m, "مانده انبار بدون طرف مقابل")
        ]);

        var action = async () => await NewOpeningService(unbalancedSource).CreateOpeningVoucherAsync(
            new OpeningVoucherCommand(_harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.OpeningUnbalanced");
    }

    // ------------------------------------------------------------------
    // انتقال مانده اشخاص/تفصیلی‌ها
    // ------------------------------------------------------------------

    [Fact]
    public async Task Opening_ShouldCarryPersonDimensionBalances()
    {
        var customer = await CreateDimensionAsync(FinancialDimensionType.Customer, "CU-OP", "مشتری افتتاحیه");

        // وصول از مشتری: بانک بدهکار ۴۵۰ با بعد مشتری / درآمد بستانکار
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, new DateOnly(2025, 2, 10), "فروش به مشتری",
            [
                new VoucherLineCommand(_bank.Id, DebitCredit.Debit, 450m, "وصول از مشتری",
                    Dimensions: [new DimensionAssignmentCommand(FinancialDimensionType.Customer, customer.Id)]),
                new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 450m, "فروش به مشتری")
            ],
            DocumentType.General, null, null, null, null, null, "user"));
        await _svc.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));

        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        await NewOpeningService().CreateOpeningVoucherAsync(new OpeningVoucherCommand(
            _harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        // مانده بعد مشتری باید در سال ۲ با همان بعد ادامه یابد
        using var db = _harness.NewDbContext();
        var year2EntryIds = await db.GeneralLedgerEntries
            .Where(e => e.TenantId == _harness.TenantId && e.CompanyId == _harness.CompanyId
                        && e.FiscalYearId == _year2.Id)
            .Select(e => e.Id)
            .ToListAsync();
        var carriedDimension = await db.GeneralLedgerEntryDimensions
            .SingleOrDefaultAsync(d => d.TenantId == _harness.TenantId
                                       && d.CompanyId == _harness.CompanyId
                                       && d.DimensionId == customer.Id
                                       && year2EntryIds.Contains(d.GeneralLedgerEntryId));
        carriedDimension.Should().NotBeNull("مانده اشخاص باید با بعد مشتری به سال بعد منتقل شود");
    }

    private async Task<FinancialDimensionDto> CreateDimensionAsync(FinancialDimensionType type, string code, string name)
    {
        using var db = _harness.NewDbContext();
        var service = new FinancialDimensionService(db);
        return await service.CreateAsync(new ERP.Modules.Accounting.Application.Services.CreateFinancialDimensionCommand(
            _harness.TenantId, _harness.CompanyId, type, code, name, "admin"));
    }

    // ------------------------------------------------------------------
    // عدم تطابق افتتاحیه و اختتامیه: افتتاحیه پیش از بسته شدن سال قبل ممنوع
    // ------------------------------------------------------------------

    [Fact]
    public async Task Opening_BeforePreviousYearClosed_ShouldReject()
    {
        await PostPair(_capital, _cash, 100m, new DateOnly(2025, 1, 5));

        var action = async () => await NewOpeningService().CreateOpeningVoucherAsync(
            new OpeningVoucherCommand(_harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.OpeningRequiresClosedPreviousYear");
    }

    // ------------------------------------------------------------------
    // جلوگیری از افتتاحیه تکراری
    // ------------------------------------------------------------------

    [Fact]
    public async Task DuplicateOpening_ShouldReject()
    {
        await PostPair(_capital, _cash, 1000m, new DateOnly(2025, 1, 5));
        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        await NewOpeningService().CreateOpeningVoucherAsync(new OpeningVoucherCommand(
            _harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        var action = async () => await NewOpeningService().CreateOpeningVoucherAsync(
            new OpeningVoucherCommand(_harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.OpeningAlreadyExists");
    }

    // ------------------------------------------------------------------
    // صفر شدن حساب موقت + انتقال سود به مقصد + محاسبه سود و زیان
    // ------------------------------------------------------------------

    [Fact]
    public async Task Closing_ShouldZeroTemporaryAccountsAndTransferNetIncome()
    {
        await PostYear1Doc(1000m, 1000m, new DateOnly(2025, 2, 1));
        await PostYear1Doc(400m, 400m, new DateOnly(2025, 3, 1));
        await PostPair(_expense, _cash, 400m, new DateOnly(2025, 3, 15));

        var result = await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        using (new AssertionScope())
        {
            result.Status.Should().Be(FiscalClosingStatus.Closed);
            result.TotalRevenue.Should().Be(1400m);
            result.TotalExpense.Should().Be(400m);
            result.NetIncome.Should().Be(1000m);
            result.ClosingDocumentNumber.Should().NotBeNull();

            // حساب‌های موقت صفر شده‌اند
            (await GetSignedBalanceAsync(_revenue.Id, _year1.Id)).Should().Be(0m, "درآمد پس از اختتامیه صفر است");
            (await GetSignedBalanceAsync(_expense.Id, _year1.Id)).Should().Be(0m, "هزینه پس از اختتامیه صفر است");

            // نتیجه به حساب مقصد منتقل شده (بستانکار → مانده علامت‌دار منفی)
            (await GetSignedBalanceAsync(_retainedEarnings.Id, _year1.Id)).Should().Be(-1000m);

            (await GetFiscalYearStatusAsync(_year1.Id)).Should().Be(FiscalYearStatus.Closed);
        }
    }

    // ------------------------------------------------------------------
    // اختتامیه ناموفق: سند Draft باقی‌مانده + قابل بازیابی بودن سابقه
    // ------------------------------------------------------------------

    [Fact]
    public async Task Closing_WithUnfinalizedDraft_ShouldFailWithRecordedSteps()
    {
        await PostPair(_capital, _cash, 100m, new DateOnly(2025, 1, 5));

        // سند Draft باقی‌مانده
        await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, new DateOnly(2025, 2, 1), "سند نیمه‌کاره",
            [
                new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 50m, "نیمه‌کاره"),
                new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 50m, "نیمه‌کاره")
            ],
            DocumentType.General, null, null, null, null, null, "user"));

        var result = await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        using (new AssertionScope())
        {
            result.Status.Should().Be(FiscalClosingStatus.Failed);
            result.FailedReason.Should().Contain("ثبت‌نشده");
            result.Steps.Should().HaveCount(3, "سه مرحله اعتبارسنجی اجرا شده و ادامه متوقف شده");
            result.Steps.First(s => s.StepNumber == 2).Status.Should().Be(FiscalClosingStepStatus.Failed);
            result.Steps.First(s => s.StepNumber == 1).Output.Should().NotBeNullOrEmpty("خروجی هر مرحله ثبت می‌شود");

            _year1.Status.Should().Be(FiscalYearStatus.Open, "سال با اختتامیه ناموفق بسته نمی‌شود");
        }

        // بازیابی: پس از نهایی کردن سند، اجرای مجدد موفق است
        var draft = await _svc.Db.Documents
            .FirstAsync(d => d.TenantId == _harness.TenantId && d.Status == DocumentStatus.Draft);
        await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));

        var retry = await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        retry.Status.Should().Be(FiscalClosingStatus.Closed, "اجرای مجدد پس از رفع مشکل موفق است");
        retry.Steps.Should().OnlyContain(s => s.Status == FiscalClosingStepStatus.Completed);
    }

    // ------------------------------------------------------------------
    // اختتامیه تکراری
    // ------------------------------------------------------------------

    [Fact]
    public async Task DuplicateClosing_ShouldReject()
    {
        await PostPair(_capital, _cash, 100m, new DateOnly(2025, 1, 5));
        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        var action = async () => await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.ClosingAlreadyDone");
    }

    // ------------------------------------------------------------------
    // معکوس‌سازی سند و معکوس‌سازی تکراری
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reversal_ShouldMirrorAndSecondReversal_ShouldReject()
    {
        var docId = await PostYear1Doc(250m, 250m, new DateOnly(2025, 2, 10));

        var reversal = await _svc.Engine.ReverseAsync(new ReverseVoucherCommand(
            _harness.TenantId, _harness.CompanyId, docId, new DateOnly(2025, 2, 11),
            "برگشت سند — علت: اشتباه مبلغ", null, "user"));

        using (new AssertionScope())
        {
            reversal.ReversalDocumentNumber.Should().BeGreaterThan(0);
            (await GetSignedBalanceAsync(_cash.Id, _year1.Id)).Should().Be(0m, "معکوس مانده را خنثی می‌کند");

            var original = await _svc.Db.Documents.AsNoTracking().SingleAsync(d => d.Id == docId);
            original.Status.Should().Be(DocumentStatus.Reversed);
            original.ReversedDocumentId.Should().Be(reversal.ReversalDocumentId);
        }

        // معکوس‌سازی تکراری غیرمجاز: Idempotent است — همان سند معکوس بازمی‌گردد، سند دومی ساخته نمی‌شود
        var secondReversal = await _svc.Engine.ReverseAsync(new ReverseVoucherCommand(
            _harness.TenantId, _harness.CompanyId, docId, new DateOnly(2025, 2, 12),
            "برگشت دوباره", null, "user"));

        secondReversal.WasAlreadyReversed.Should().BeTrue("معکوس‌سازی تکراری سند جدید ایجاد نمی‌کند");
        secondReversal.ReversalDocumentId.Should().Be(reversal.ReversalDocumentId);

        // فقط یک سند معکوس برای سند اصلی وجود دارد
        var reversalCount = await _svc.Db.Documents
            .AsNoTracking()
            .CountAsync(d => d.ReferenceDocumentId == docId && d.DocumentType == DocumentType.Reversal);
        reversalCount.Should().Be(0); // سند معکوس با ReversedDocumentId اصلی متصل است نه Reference
        var linkedReversals = await _svc.Db.Documents
            .AsNoTracking()
            .CountAsync(d => d.Id == reversal.ReversalDocumentId && d.DocumentType == DocumentType.Reversal);
        linkedReversals.Should().Be(1);
    }

    // ------------------------------------------------------------------
    // سند اصلاحی متصل به مرجع بدون بازنویسی سند اصلی
    // ------------------------------------------------------------------

    [Fact]
    public async Task Adjustment_ShouldLinkToSourceWithoutRewritingIt()
    {
        var sourceId = await PostYear1Doc(500m, 500m, new DateOnly(2025, 3, 1));

        var original = await _svc.Db.Documents.AsNoTracking().SingleAsync(d => d.Id == sourceId);
        var originalTotal = original.TotalDebit;
        var originalLines = original.Lines.Count;

        var adjustment = await NewAdjustmentService().CreateAdjustmentVoucherAsync(new AdjustmentVoucherCommand(
            _harness.TenantId, _harness.CompanyId, sourceId, new DateOnly(2025, 3, 20),
            "اصلاح طبقه‌بندی حساب: بخشی از فروش باید در حساب بانک ثبت می‌شد",
            [
                new VoucherLineCommand(_bank.Id, DebitCredit.Debit, 100m, "اصلاح — انتقال به بانک"),
                new VoucherLineCommand(_cash.Id, DebitCredit.Credit, 100m, "اصلاح — کاهش صندوق")
            ],
            "accountant"));

        using (new AssertionScope())
        {
            var adjustmentEntity = await _svc.Db.Documents.AsNoTracking()
                .SingleAsync(d => d.Id == adjustment.Id);
            adjustmentEntity.DocumentType.Should().Be(DocumentType.Adjustment);
            adjustmentEntity.Status.Should().Be(DocumentStatus.Posted, "سند اصلاحی با AutoPost قطعی می‌شود");
            adjustmentEntity.ReferenceDocumentId.Should().Be(sourceId, "سند اصلاحی باید به مرجع متصل باشد");

            // سند اصلی دست‌نخورده است
            var after = await _svc.Db.Documents.AsNoTracking().SingleAsync(d => d.Id == sourceId);
            after.TotalDebit.Should().Be(originalTotal);
            after.Lines.Count.Should().Be(originalLines);
            after.Status.Should().Be(DocumentStatus.Posted);

            // سابقه ممیزی اصلاح ثبت شده است
            var audit = await _svc.Db.DocumentAuditLogs
                .Where(l => l.DocumentId == adjustment.Id && l.Action == DocumentAuditActions.Created)
                .ToListAsync();
            audit.Should().Contain(l => l.Details!.Contains("اصلاح"));
        }
    }

    [Fact]
    public async Task Adjustment_WithoutReason_ShouldReject()
    {
        var sourceId = await PostYear1Doc(100m, 100m, new DateOnly(2025, 3, 1));

        var action = async () => await NewAdjustmentService().CreateAdjustmentVoucherAsync(new AdjustmentVoucherCommand(
            _harness.TenantId, _harness.CompanyId, sourceId, new DateOnly(2025, 3, 20),
            "  ",
            [
                new VoucherLineCommand(_bank.Id, DebitCredit.Debit, 10m, "اصلاح"),
                new VoucherLineCommand(_cash.Id, DebitCredit.Credit, 10m, "اصلاح")
            ],
            "accountant"));

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.AdjustmentReasonRequired");
    }

    // ------------------------------------------------------------------
    // بازگشایی با مجوز / بدون مجوز
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reopening_WithApproval_ShouldRecordFullAuditAndKeepPeriodsClosed()
    {
        await PostPair(_capital, _cash, 300m, new DateOnly(2025, 1, 5));
        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        // اگر افتتاحیه سال بعد صادر شده باشد بازگشایی ممنوع است — پس فقط سال را می‌بندیم
        var result = await NewReopeningService().ReopenAsync(new ReopenFiscalYearCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id,
            "کشف خطای ارزیابی موجودی در اسفند؛ نیاز به اصلاح اسناد",
            "controller", "cfo"));

        using (new AssertionScope())
        {
            result.RequestedBy.Should().Be("controller");
            result.ApprovedBy.Should().Be("cfo");
            result.Reason.Should().Contain("ارزیابی موجودی");

            (await GetFiscalYearStatusAsync(_year1.Id)).Should().Be(FiscalYearStatus.Open, "سال پس از بازگشایی باز است");

            // اما دوره‌ها بسته می‌مانند: پذیرش سند بدون کنترل ممنوع
            using var verifyDb = _harness.NewDbContext();
            var periods = await verifyDb.FiscalPeriods
                .Where(p => p.FiscalYearId == _year1.Id)
                .Select(p => p.Status)
                .ToListAsync();
            periods.Should().OnlyContain(st => st == FiscalPeriodStatus.Closed);

            // اسناد قطعی همچنان تغییرناپذیر
            var posted = await _svc.Db.Documents.AsNoTracking()
                .FirstAsync(d => d.Status == DocumentStatus.Posted && d.FiscalYearId == _year1.Id);
            var action = async () => await _svc.Docs.UpdateDraftAsync(new UpdateVoucherDraftCommand(
                _harness.TenantId, _harness.CompanyId, posted.Id, new DateOnly(2025, 1, 10), "دستکاری",
                [
                    new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 999m, "دستکاری"),
                    new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 999m, "دستکاری")
                ],
                null,
                "attacker"));
            (await action.Should().ThrowAsync<AccountingDomainException>())
                .Which.Code.Should().Be("Accounting.DocumentNotEditable");

            // سابقه بازگشایی کامل
            var history = await NewReopeningService().GetHistoryAsync(
                _harness.TenantId, _harness.CompanyId, _year1.Id);
            history.Should().HaveCount(1);
            history[0].NextOpeningImpact.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Reopening_WithoutProperApproval_ShouldReject()
    {
        await PostPair(_capital, _cash, 300m, new DateOnly(2025, 1, 5));
        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));

        // تأییدکننده همان درخواست‌دهنده
        var selfApproval = async () => await NewReopeningService().ReopenAsync(new ReopenFiscalYearCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, "دلیل", "same-user", "same-user"));
        (await selfApproval.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.ReopeningSelfApproval");

        // بدون دلیل
        var noReason = async () => await NewReopeningService().ReopenAsync(new ReopenFiscalYearCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, "", "controller", "cfo"));
        (await noReason.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.ReopeningReasonRequired");
    }

    [Fact]
    public async Task Reopening_WhenNextYearOpeningExists_ShouldReject()
    {
        await PostPair(_capital, _cash, 300m, new DateOnly(2025, 1, 5));
        await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));
        await NewOpeningService().CreateOpeningVoucherAsync(new OpeningVoucherCommand(
            _harness.TenantId, _harness.CompanyId, _year2.Id, "accountant"));

        var action = async () => await NewReopeningService().ReopenAsync(new ReopenFiscalYearCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, "دلیل اصلاح", "controller", "cfo"));

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.ReopeningBlockedByNextOpening");
    }

    // ------------------------------------------------------------------
    // Audit: سند اختتامیه و افتتاحیه در چرخه باید سابقه ممیزی داشته باشند
    // ------------------------------------------------------------------

    [Fact]
    public async Task Cycle_ShouldLeaveCompleteAuditTrail()
    {
        await PostPair(_capital, _cash, 1000m, new DateOnly(2025, 1, 5));
        await PostYear1Doc(400m, 400m, new DateOnly(2025, 2, 5));
        await PostPair(_expense, _cash, 150m, new DateOnly(2025, 3, 5));

        var closing = await NewClosingService().RunClosingAsync(new RunFiscalClosingCommand(
            _harness.TenantId, _harness.CompanyId, _year1.Id, _retainedEarnings.Id, "cfo"));
        closing.ClosingDocumentId.Should().NotBeNull();

        using var db = _harness.NewDbContext();
        var closingAudit = await db.DocumentAuditLogs
            .Where(l => l.DocumentId == closing.ClosingDocumentId && l.Action == DocumentAuditActions.Posted)
            .ToListAsync();
        closingAudit.Should().NotBeEmpty("سند اختتامیه باید سابقه ممیزی ثبت قطعی داشته باشد");

        // گردش‌کار با مراحل زمان‌دار ثبت شده است
        var workflow = await db.FiscalYearClosings
            .Include(c => c.Steps)
            .SingleAsync(c => c.FiscalYearId == _year1.Id);
        workflow.Steps.Should().HaveCount(6);
        workflow.Steps.Should().OnlyContain(s => s.Status == FiscalClosingStepStatus.Completed);
        workflow.Steps.Should().OnlyContain(s => s.FinishedAtUtc != null);
        workflow.ClosedBy.Should().Be("cfo");
        workflow.NetIncome.Should().Be(250m);
    }

    public void Dispose() => _harness.Dispose();
}

/// <summary>منبع انبار آزمون برای تزریق مانده دلخواه.</summary>
public class FakeInventorySource(IReadOnlyList<OpeningInventoryLine> lines) : IOpeningInventorySource
{
    public Task<IReadOnlyList<OpeningInventoryLine>> GetOpeningLinesAsync(
        Guid tenantId, Guid companyId, Guid targetFiscalYearId, CancellationToken cancellationToken = default)
        => Task.FromResult(lines);
}
