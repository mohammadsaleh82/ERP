using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Dv = ERP.Modules.Accounting.Application.Services.DimensionValidationApplicationService;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای اجباری PROMPT 06: تفصیلی شناور و ابعاد مالی.
/// معیار پایان: ثبت و گزارش‌گیری مالی بر اساس تفصیلی و ابعاد مالی بدون نقض مالکیت ماژول‌ها.
/// </summary>
public class FinancialDimensionIntegrationTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _fiscalYear;
    private readonly Account _cash;
    private readonly Account _bank;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _services;
    private readonly IFinancialDimensionService _dimensions;
    private readonly IDimensionAnalysisService _analysis;
    private readonly Dv _dimensionValidation;

    public FinancialDimensionIntegrationTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _fiscalYear = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _bank = _harness.SeedPostingAccount("1002", "بانک ملت");
        _services = _harness.NewServices();

        var queryDb = _harness.NewDbContext();
        _dimensions = new FinancialDimensionService(queryDb);
        _analysis = new DimensionAnalysisService(queryDb);
        _dimensionValidation = new Dv(queryDb);
    }

    private async Task<DocumentDto> CreateDraftAsync(
        Account debitAccount,
        Account creditAccount,
        decimal amount,
        DateOnly date,
        IReadOnlyList<DimensionAssignmentCommand>? debitDimensions = null,
        IReadOnlyList<DimensionAssignmentCommand>? creditDimensions = null)
    {
        var debitLine = new VoucherLineCommand(debitAccount.Id, DebitCredit.Debit, amount, "طرف بدهکار", Dimensions: debitDimensions ?? []);
        var creditLine = new VoucherLineCommand(creditAccount.Id, DebitCredit.Credit, amount, "طرف بستانکار", Dimensions: creditDimensions ?? []);

        return await _services.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, date, "سند ابعاد مالی",
            [debitLine, creditLine], DocumentType.General, null, null, null, null, null, "user"));
    }

    private DimensionAssignmentCommand Dim(FinancialDimensionType type, Guid id) => new(type, id);

    /// <summary>سرویس ابعاد با زمینه بدون شرکت فعال (برای seed شرکت/مستاجر دیگر بدون گارد جهش).</summary>
    private IFinancialDimensionService UnarmedDimensionService()
    {
        var seedTenantContext = new BuildingBlocks.Tenancy.TenantContext();
        seedTenantContext.SetContext(_harness.TenantId, null);
        var db = new AccountingDbContext(_harness.Options, seedTenantContext, null, _harness.Clock);
        return new FinancialDimensionService(db);
    }

    // ------------------------------------------------------------------
    // سناریو ۱: تفصیلی معتبر — ثبت با تفصیلی و انعکاس در دفتر کل
    // ------------------------------------------------------------------

    [Fact]
    public async Task ValidDetailDimension_ShouldPostAndMirrorIntoGeneralLedger()
    {
        var customer = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.DetailAccount, "C-101", "مشتری الف", "admin"));

        var draft = await CreateDraftAsync(
            _cash, _bank, 500m, new DateOnly(2025, 1, 10),
            debitDimensions: [Dim(FinancialDimensionType.DetailAccount, customer.Id)]);

        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));

        var glDimension = await _services.Db.GeneralLedgerEntryDimensions
            .SingleOrDefaultAsync(d => d.DimensionType == FinancialDimensionType.DetailAccount && d.DimensionId == customer.Id);

        glDimension.Should().NotBeNull("بعد مالی ردیف باید به سطر دفتر کل همان ردیف منعکس شود");
        var glEntry = await _services.Db.GeneralLedgerEntries
            .SingleAsync(e => e.Id == glDimension!.GeneralLedgerEntryId);
        glEntry.DebitAmount.Should().Be(500m, "انعکاس ابعاد باید روی سطر بدهکار همان ردیف باشد");
        glDimension.DocumentId.Should().Be(draft.Id);
    }

    // ------------------------------------------------------------------
    // سناریو ۲ و ۳: تفصیلی نامعتبر و غیرفعال
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnknownDimension_ShouldRejectDraft()
    {
        var action = async () => await CreateDraftAsync(
            _cash, _bank, 100m, new DateOnly(2025, 1, 10),
            debitDimensions: [Dim(FinancialDimensionType.CostCenter, Guid.NewGuid())]);

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionNotFound");
    }

    [Fact]
    public async Task InactiveDimension_ShouldRejectDraft()
    {
        var project = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.Project, "P-1", "پروژه تعلیق", "admin"));

        await _dimensions.DeactivateAsync(_harness.TenantId, _harness.CompanyId, project.Id, "admin");

        var action = async () => await CreateDraftAsync(
            _cash, _bank, 100m, new DateOnly(2025, 1, 10),
            debitDimensions: [Dim(FinancialDimensionType.Project, project.Id)]);

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionInactive");
    }

    // ------------------------------------------------------------------
    // سناریو ۴: تفصیلی شرکت دیگر
    // ------------------------------------------------------------------

    [Fact]
    public async Task DimensionOfAnotherCompany_ShouldRejectDraft()
    {
        var otherCompanyId = Guid.NewGuid();
        var dimension = await UnarmedDimensionService().CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, otherCompanyId, FinancialDimensionType.CostCenter, "CC-FOREIGN", "مرکز هزینه شرکت دیگر", "admin"));

        var action = async () => await CreateDraftAsync(
            _cash, _bank, 100m, new DateOnly(2025, 1, 10),
            debitDimensions: [Dim(FinancialDimensionType.CostCenter, dimension.Id)]);

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionForeignCompany");
    }

    // ------------------------------------------------------------------
    // سناریو ۵: تفصیلی اجباری
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequiredDetailAccount_ShouldRejectMissingAndAcceptPresent()
    {
        // حساب پرداختنی با قاعده «بدون تفصیلی قابل ثبت نیست»
        using (var setupDb = _harness.NewDbContext())
        {
            var payable = await setupDb.Accounts.SingleAsync(a => a.Id == _bank.Id);
            payable.RequireDetailAccount = true;
            await setupDb.SaveChangesAsync();
        }

        var supplier = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.DetailAccount, "S-1", "تأمین‌کننده الف", "admin"));

        // بدون تفصیلی → رد
        var missing = async () => await CreateDraftAsync(
            _cash, _bank, 300m, new DateOnly(2025, 1, 10));
        (await missing.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionRequired");

        // با تفصیلی → پذیرش و ذخیره اختصاص در ردیف
        var draft = await CreateDraftAsync(
            _cash, _bank, 300m, new DateOnly(2025, 1, 10),
            creditDimensions: [Dim(FinancialDimensionType.DetailAccount, supplier.Id)]);

        var persistedLineDimension = await _services.Db.DocumentLineDimensions
            .SingleOrDefaultAsync(d => d.DocumentId == draft.Id && d.DimensionId == supplier.Id);
        persistedLineDimension.Should().NotBeNull("اختصاص تفصیلی باید در ردیف سند ذخیره شود");

        var posted = await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));
        posted.DocumentNumber.Should().BeGreaterThan(0);
    }

    // ------------------------------------------------------------------
    // سناریو ۶: Dimension ترکیبی — چند بعد اجباری هم‌زمان
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultipleRequiredDimensions_ShouldPostWithBothAssigned()
    {
        // سیاست‌ها از طریق سرویس عمومی (افزودن مستقیم به DbSet) تعریف می‌شوند
        await _dimensions.SetAccountPolicyAsync(new SetAccountDimensionPolicyCommand(
            _harness.TenantId, _harness.CompanyId, _cash.Id, FinancialDimensionType.CostCenter, DimensionPolicyMode.Required, "admin"));
        await _dimensions.SetAccountPolicyAsync(new SetAccountDimensionPolicyCommand(
            _harness.TenantId, _harness.CompanyId, _cash.Id, FinancialDimensionType.Project, DimensionPolicyMode.Required, "admin"));

        var costCenter = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.CostCenter, "CC-10", "واحد فروش", "admin"));
        var project = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.Project, "PJ-5", "پروژه توسعه", "admin"));

        // فقط یکی → رد
        var incomplete = async () => await CreateDraftAsync(
            _cash, _bank, 200m, new DateOnly(2025, 1, 12),
            debitDimensions: [Dim(FinancialDimensionType.CostCenter, costCenter.Id)]);
        (await incomplete.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionRequired");

        // هر دو → ثبت و انعکاس هر دو در دفتر کل
        var draft = await CreateDraftAsync(
            _cash, _bank, 200m, new DateOnly(2025, 1, 12),
            debitDimensions:
            [
                Dim(FinancialDimensionType.CostCenter, costCenter.Id),
                Dim(FinancialDimensionType.Project, project.Id)
            ]);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));

        var glTypes = await _services.Db.GeneralLedgerEntryDimensions
            .Where(d => d.DocumentId == draft.Id)
            .Select(d => d.DimensionType)
            .Distinct()
            .ToListAsync();
        glTypes.Should().BeEquivalentTo([FinancialDimensionType.CostCenter, FinancialDimensionType.Project],
            "هر دو بعد اجباری باید در سطرهای دفتر کل منعکس شوند");
    }

    // ------------------------------------------------------------------
    // سناریو ۷: ترکیب غیرمجاز
    // ------------------------------------------------------------------

    [Fact]
    public async Task ForbiddenCombination_ShouldRejectDraft()
    {
        var cc1 = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.CostCenter, "CC-A", "مرکز A", "admin"));
        var projectX = await _dimensions.CreateAsync(
            new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.Project, "PJ-X", "پروژه X", "admin"));

        await _dimensions.AddCombinationRuleAsync(new AddDimensionCombinationRuleCommand(
            _harness.TenantId, _harness.CompanyId,
            FinancialDimensionType.CostCenter, cc1.Id,
            FinancialDimensionType.Project, projectX.Id,
            "مرکز A با پروژه X ترکیب نمی‌شود", "admin"));

        var action = async () => await CreateDraftAsync(
            _cash, _bank, 150m, new DateOnly(2025, 1, 15),
            debitDimensions:
            [
                Dim(FinancialDimensionType.CostCenter, cc1.Id),
                Dim(FinancialDimensionType.Project, projectX.Id)
            ]);

        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionCombinationForbidden");
    }

    // ------------------------------------------------------------------
    // سناریوهای ۸ تا ۱۰: تحلیل مالی بر اساس مشتری، پروژه و مرکز هزینه
    // ------------------------------------------------------------------

    [Fact]
    public async Task FinancialAnalysis_ShouldReportTurnoverByCustomerProjectAndCostCenter()
    {
        var customer1 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Customer, "CU-1", "مشتری ۱", "admin"));
        var customer2 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Customer, "CU-2", "مشتری ۲", "admin"));
        var project1 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Project, "PJ-1", "پروژه ۱", "admin"));
        var costCenter1 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.CostCenter, "CC-1", "مرکز هزینه ۱", "admin"));

        // مشتری ۱: بدهکار ۴۰۰؛ مشتری ۲: بدهکار ۲۵۰؛ پروژه ۱: بستانکار ۴۰۰؛ مرکز هزینه ۱: بستانکار ۶۵۰
        await PostWithDimensions(_cash, _bank, 400m, new DateOnly(2025, 1, 10),
            [Dim(FinancialDimensionType.Customer, customer1.Id), Dim(FinancialDimensionType.Project, project1.Id), Dim(FinancialDimensionType.CostCenter, costCenter1.Id)], null);
        await PostWithDimensions(_cash, _bank, 250m, new DateOnly(2025, 1, 11),
            [Dim(FinancialDimensionType.Customer, customer2.Id), Dim(FinancialDimensionType.CostCenter, costCenter1.Id)], null);

        var customerReport = await _analysis.GetTurnoverByDimensionAsync(_harness.TenantId, _harness.CompanyId,
            new DimensionTurnoverQuery { DimensionType = FinancialDimensionType.Customer, FiscalYearId = _fiscalYear.Id });
        customerReport.TotalDebit.Should().Be(650m);
        customerReport.Rows.Should().HaveCount(2);
        var customer1Row = customerReport.Rows.Single(r => r.DimensionId == customer1.Id);
        customer1Row.Debit.Should().Be(400m);
        customer1Row.SignedBalance.Should().Be(400m);
        customer1Row.DimensionName.Should().Be("مشتری ۱");

        var projectReport = await _analysis.GetTurnoverByDimensionAsync(_harness.TenantId, _harness.CompanyId,
            new DimensionTurnoverQuery { DimensionType = FinancialDimensionType.Project, FiscalYearId = _fiscalYear.Id });
        projectReport.Rows.Single(r => r.DimensionId == project1.Id).Debit.Should().Be(400m, "بعد پروژه به ردیف بدهکار اختصاص داشت");

        var costCenterReport = await _analysis.GetTurnoverByDimensionAsync(_harness.TenantId, _harness.CompanyId,
            new DimensionTurnoverQuery { DimensionType = FinancialDimensionType.CostCenter, FiscalYearId = _fiscalYear.Id });
        costCenterReport.Rows.Single(r => r.DimensionId == costCenter1.Id).Debit.Should().Be(650m, "جمع بدهکار هر دو سند مرکز هزینه");

        // گردش یک حساب مشخص بر اساس مشتری (تفکیک حساب)
        var cashByCustomer = await _analysis.GetAccountTurnoverByDimensionAsync(_harness.TenantId, _harness.CompanyId, _cash.Id,
            new DimensionTurnoverQuery { DimensionType = FinancialDimensionType.Customer, FiscalYearId = _fiscalYear.Id });
        cashByCustomer.Rows.Should().HaveCount(2);
        cashByCustomer.Rows.Single(r => r.DimensionId == customer2.Id).Debit.Should().Be(250m);
    }

    // ------------------------------------------------------------------
    // گزارش ترکیبی چندبعدی
    // ------------------------------------------------------------------

    [Fact]
    public async Task CombinedMultiDimensionalReport_ShouldGroupByDimensionCombination()
    {
        var cc1 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.CostCenter, "CC-R1", "مرکز R1", "admin"));
        var cc2 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.CostCenter, "CC-R2", "مرکز R2", "admin"));
        var project1 = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(_harness.TenantId, _harness.CompanyId, FinancialDimensionType.Project, "PJ-R1", "پروژه R1", "admin"));

        await PostWithDimensions(_cash, _bank, 100m, new DateOnly(2025, 1, 10),
            [Dim(FinancialDimensionType.CostCenter, cc1.Id), Dim(FinancialDimensionType.Project, project1.Id)], null);
        await PostWithDimensions(_cash, _bank, 50m, new DateOnly(2025, 1, 11),
            [Dim(FinancialDimensionType.CostCenter, cc1.Id), Dim(FinancialDimensionType.Project, project1.Id)], null);
        await PostWithDimensions(_cash, _bank, 70m, new DateOnly(2025, 1, 12),
            [Dim(FinancialDimensionType.CostCenter, cc2.Id), Dim(FinancialDimensionType.Project, project1.Id)], null);

        var report = await _analysis.GetCombinedAnalysisAsync(_harness.TenantId, _harness.CompanyId,
            new CombinedDimensionQuery
            {
                DimensionTypes = [FinancialDimensionType.CostCenter, FinancialDimensionType.Project],
                FiscalYearId = _fiscalYear.Id
            });

        report.IsTruncated.Should().BeFalse();
        report.TotalDebit.Should().Be(220m);
        report.Rows.Should().HaveCount(2, "ترکیب (CC1,PJ1) و (CC2,PJ1)");

        var cc1Row = report.Rows.Single(r => r.Dimensions.First(d => d.DimensionType == FinancialDimensionType.CostCenter).DimensionId == cc1.Id);
        cc1Row.Debit.Should().Be(150m, "تجمیع دو سند هم‌ترکیب بدون شمارش دوباره");
        cc1Row.Dimensions.Should().OnlyContain(d => !string.IsNullOrEmpty(d.Code), "نام/کد ابعاد باید در گزارش پر شود");
    }

    // ------------------------------------------------------------------
    // سناریو ۱۱: جداسازی Tenant
    // ------------------------------------------------------------------

    [Fact]
    public async Task TenantIsolation_ShouldHideDimensionsOfOtherTenants()
    {
        var otherTenantId = Guid.NewGuid();
        var foreignDimension = await new FinancialDimensionService(new AccountingDbContext(
                _harness.Options, new BuildingBlocks.Tenancy.TenantContext(), null, _harness.Clock))
            .CreateAsync(new CreateFinancialDimensionCommand(
                otherTenantId, _harness.CompanyId, FinancialDimensionType.Customer, "CU-FOREIGN", "مشتری مستاجر دیگر", "admin"));

        // فهرست ابعاد مستاجر فعلی نباید بعد مستاجر دیگر را برگرداند
        var (items, _) = await _dimensions.ListAsync(_harness.TenantId, _harness.CompanyId, new FinancialDimensionListQuery { PageSize = 50 });
        items.Should().NotContain(d => d.Id == foreignDimension.Id);

        // تحلیل مستاجر فعلی گردش بعد مستاجر دیگر را ندارد
        var report = await _analysis.GetTurnoverByDimensionAsync(_harness.TenantId, _harness.CompanyId,
            new DimensionTurnoverQuery { DimensionType = FinancialDimensionType.Customer, DimensionId = foreignDimension.Id, FiscalYearId = _fiscalYear.Id });
        report.Rows.Should().BeEmpty();
        report.TotalDebit.Should().Be(0m);
    }

    // ------------------------------------------------------------------
    // قرارداد بین‌ماژولی: ثبت Idempotent از ماژول مالک (بدون رفرنس داخلی)
    // ------------------------------------------------------------------

    [Fact]
    public async Task DimensionContract_ShouldUpsertExternalReferenceIdempotently()
    {
        var api = new FinancialDimensionApi(_dimensions);
        var partyId = Guid.NewGuid(); // شناسه Party در ماژول Parties (مالک داده)

        var first = await api.RegisterAsync(new RegisterFinancialDimensionRequest(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Party, partyId, "PT-9", "شخص حقوقی ۹", "Parties", "system"));
        first.IsActive.Should().BeTrue();
        first.ExternalReferenceId.Should().Be(partyId);
        first.OwnerModule.Should().Be("Parties");

        // ثبت دوم با همان مرجع → Upsert، نه ردیف جدید
        var second = await api.RegisterAsync(new RegisterFinancialDimensionRequest(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Party, partyId, "PT-9", "شخص حقوقی ۹ (اصلاح نام)", "Parties", "system"));
        second.Id.Should().Be(first.Id);
        second.Name.Should().Be("شخص حقوقی ۹ (اصلاح نام)");

        var count = await _services.Db.FinancialDimensions
            .CountAsync(d => d.ExternalReferenceId == partyId);
        count.Should().Be(1, "قرارداد ثبت Idempotent دارد");

        // غیرفعال‌سازی از سمت مالک → غیرقابل استفاده در اسناد
        await api.SyncAsync(new SyncFinancialDimensionRequest(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Party, partyId, null, null, IsActive: false, "system"));

        var action = async () => await CreateDraftAsync(
            _cash, _bank, 90m, new DateOnly(2025, 1, 20),
            debitDimensions: [Dim(FinancialDimensionType.Party, first.Id)]);
        (await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DimensionInactive");
    }

    // ------------------------------------------------------------------
    // سند معکوس باید ابعاد را نیز منعکس کند
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reversal_ShouldMirrorDimensionsOfOriginalLines()
    {
        var project = await _dimensions.CreateAsync(new CreateFinancialDimensionCommand(
            _harness.TenantId, _harness.CompanyId, FinancialDimensionType.Project, "PJ-REV", "پروژه برگشتی", "admin"));

        var draft = await CreateDraftAsync(_cash, _bank, 400m, new DateOnly(2025, 2, 1),
            debitDimensions: [Dim(FinancialDimensionType.Project, project.Id)]);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));

        var reversal = await _services.Engine.ReverseAsync(new ReverseVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, new DateOnly(2025, 2, 2), null, null, "user"));

        var reversalDimensionCount = await _services.Db.GeneralLedgerEntryDimensions
            .CountAsync(d => d.DocumentId == reversal.ReversalDocumentId && d.DimensionId == project.Id);
        reversalDimensionCount.Should().Be(1, "سند معکوس باید بعد مالی سند اصلی را کامل منعکس کند");
    }

    private async Task PostWithDimensions(
        Account debitAccount,
        Account creditAccount,
        decimal amount,
        DateOnly date,
        IReadOnlyList<DimensionAssignmentCommand>? debitDimensions,
        IReadOnlyList<DimensionAssignmentCommand>? creditDimensions)
    {
        var draft = await CreateDraftAsync(debitAccount, creditAccount, amount, date, debitDimensions, creditDimensions);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, draft.Id, null, "user"));
    }

    public void Dispose() => _harness.Dispose();
}
