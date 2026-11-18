using ERP.Modules.Accounting.Application.Services.Audit;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.UnitTests;

public class FiscalYearAndSettingsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AccountingDbContext> _options;
    private readonly FinancialCalendarService _calendarService;

    public FiscalYearAndSettingsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new AccountingDbContext(_options);
        initContext.Database.EnsureCreated();

        _calendarService = new FinancialCalendarService();
    }

    [Fact]
    public void Scenario01_InvalidFiscalYear_StartDateAfterEndDate_ShouldThrow()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // تاریخ پایان قبل از تاریخ شروع
        var start = new DateOnly(2024, 12, 31);
        var end = new DateOnly(2024, 1, 1);

        var action = () => new FiscalYear(
            tenantId,
            companyId,
            yearNumber: 1403,
            name: "سال مالی معکوس",
            startDate: start,
            endDate: end);

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*پس از تاریخ شروع*");
    }

    [Fact]
    public async Task Scenario02_OverlappingFiscalYears_SameCompany_ShouldThrow()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new FiscalYearService(dbContext, _calendarService, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // 1. سال مالی اول: 1403 (2024-03-20 تا 2025-03-20)
        await service.CreateFiscalYearAsync(tenantId, companyId,
            new CreateFiscalYearRequest(1403, "سال مالی ۱۴۰۳", FiscalCalendarType.Persian));

        // 2. تلاش برای ایجاد سال مالی با بازه متداخل در همان شرکت
        var overlapAction = async () => await service.CreateFiscalYearAsync(tenantId, companyId,
            new CreateFiscalYearRequest(
                YearNumber: 1404,
                Name: "سال مالی متداخل",
                CalendarType: FiscalCalendarType.Persian,
                StartDate: new DateOnly(2024, 10, 1), // درون بازه سال 1403!
                EndDate: new DateOnly(2025, 9, 30)));

        await overlapAction.Should().ThrowAsync<AccountingDomainException>()
            .WithMessage("*همپوشانی دارد*");
    }

    [Fact]
    public void Scenario03_PeriodOutsideFiscalYearRange_ShouldThrow()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var year = new FiscalYear(
            tenantId,
            companyId,
            yearNumber: 1403,
            name: "سال مالی ۱۴۰۳",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2025, 3, 20));

        // تاریخ شروع دوره قبل از سال مالی است
        var actionBefore = () => year.AddPeriod(
            periodNumber: 1,
            name: "دوره قبل از سال",
            startDate: new DateOnly(2024, 1, 1),
            endDate: new DateOnly(2024, 4, 1));

        actionBefore.Should().Throw<AccountingDomainException>()
            .WithMessage("*خارج از محدوده سال مالی*");

        // تاریخ پایان دوره پس از سال مالی است
        var actionAfter = () => year.AddPeriod(
            periodNumber: 12,
            name: "دوره بعد از سال",
            startDate: new DateOnly(2025, 3, 1),
            endDate: new DateOnly(2025, 4, 1));

        actionAfter.Should().Throw<AccountingDomainException>()
            .WithMessage("*خارج از محدوده سال مالی*");
    }

    [Fact]
    public void Scenario04_OverlappingPeriods_InSameFiscalYear_ShouldThrow()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var year = new FiscalYear(
            tenantId,
            companyId,
            yearNumber: 1403,
            name: "سال مالی ۱۴۰۳",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2025, 3, 20));

        // دوره ۱: فروردین
        year.AddPeriod(1, "فروردین", new DateOnly(2024, 3, 20), new DateOnly(2024, 4, 19));

        // دوره ۲: متداخل با دوره ۱ (شروع در 15 فروردین)
        var action = () => year.AddPeriod(2, "اردیبهشت متداخل", new DateOnly(2024, 4, 15), new DateOnly(2024, 5, 20));

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*همپوشانی دارد*");
    }

    [Fact]
    public void Scenario05_PostingInClosedPeriod_ShouldBeForbidden()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fyId = Guid.NewGuid();

        var period = new FiscalPeriod(
            tenantId,
            companyId,
            fyId,
            periodNumber: 1,
            name: "فروردین",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2024, 4, 19));

        // ابتدا دوره باز است و باید ثبت سند را بپذیرد
        period.EnsureCanAcceptTransactions();

        // بستن دوره
        period.Close();

        // تلاش برای پذیرش سند پس از بستن دوره باید منجر به خطا شود
        var action = () => period.EnsureCanAcceptTransactions();
        action.Should().Throw<FiscalPeriodClosedException>()
            .WithMessage("*مجاز نمی‌باشد*");
    }

    [Fact]
    public void Scenario06_PersianCalendar_YearBoundaryAndMonthDays_ShouldCalculateAccurately()
    {
        // 1. تست سال عادی (1402 سال عادی است - اسفند 29 روزه)
        _calendarService.IsLeapYear(FiscalCalendarType.Persian, 1402).Should().BeFalse();
        _calendarService.GetDaysInMonth(FiscalCalendarType.Persian, 1402, 12).Should().Be(29);

        // 2. تست سال کبیسه (1403 سال کبیسه جلالی است - اسفند 30 روزه)
        _calendarService.IsLeapYear(FiscalCalendarType.Persian, 1403).Should().BeTrue();
        _calendarService.GetDaysInMonth(FiscalCalendarType.Persian, 1403, 12).Should().Be(30);

        // 3. تست ۶ ماه اول (31 روزه)
        for (var m = 1; m <= 6; m++)
        {
            _calendarService.GetDaysInMonth(FiscalCalendarType.Persian, 1403, m).Should().Be(31);
        }

        // 4. تست ۵ ماه دوم (30 روزه)
        for (var m = 7; m <= 11; m++)
        {
            _calendarService.GetDaysInMonth(FiscalCalendarType.Persian, 1403, m).Should().Be(30);
        }

        // 5. بررسی تولید خودکار ۱۲ دوره استاندارد ماهانه شمسی
        var periods = _calendarService.GenerateStandardPeriods(FiscalCalendarType.Persian, 1403, PeriodGenerationType.Monthly);
        periods.Should().HaveCount(12);
        periods[0].Name.Should().Be("فروردین");
        periods[11].Name.Should().Be("اسفند");
    }

    [Fact]
    public void Scenario07_GregorianCalendar_YearBoundaryAndLeapYears_ShouldCalculateAccurately()
    {
        // 2024 کبیسه میلادی است (فوریه 29 روز)
        _calendarService.IsLeapYear(FiscalCalendarType.Gregorian, 2024).Should().BeTrue();
        _calendarService.GetDaysInMonth(FiscalCalendarType.Gregorian, 2024, 2).Should().Be(29);

        // 2023 سال عادی میلادی است (فوریه 28 روز)
        _calendarService.IsLeapYear(FiscalCalendarType.Gregorian, 2023).Should().BeFalse();
        _calendarService.GetDaysInMonth(FiscalCalendarType.Gregorian, 2023, 2).Should().Be(28);

        // مرزهای سال میلادی
        var (start, end) = _calendarService.GetYearBounds(FiscalCalendarType.Gregorian, 2024);
        start.Should().Be(new DateOnly(2024, 1, 1));
        end.Should().Be(new DateOnly(2024, 12, 31));

        // تولید فصلی میلادی
        var quarters = _calendarService.GenerateStandardPeriods(FiscalCalendarType.Gregorian, 2024, PeriodGenerationType.Quarterly);
        quarters.Should().HaveCount(4);
        quarters[0].StartDate.Should().Be(new DateOnly(2024, 1, 1));
        quarters[0].EndDate.Should().Be(new DateOnly(2024, 3, 31));
    }

    [Fact]
    public void Scenario08_DateConversionAcrossYearBoundary()
    {
        // آغاز سال شمسی 1403: 1 فروردین 1403 = 2024-03-20
        var farvardin1 = _calendarService.ToGregorianDate(1403, 1, 1);
        farvardin1.Should().Be(new DateOnly(2024, 3, 20));

        var (y, m, d) = _calendarService.ToPersianDate(new DateOnly(2024, 3, 20));
        y.Should().Be(1403);
        m.Should().Be(1);
        d.Should().Be(1);

        // پایان سال شمسی 1403 (کبیسه 30 اسفند): 30 اسفند 1403 = 2025-03-20
        var esfand30 = _calendarService.ToGregorianDate(1403, 12, 30);
        esfand30.Should().Be(new DateOnly(2025, 3, 20));

        var (ey, em, ed) = _calendarService.ToPersianDate(new DateOnly(2025, 3, 20));
        ey.Should().Be(1403);
        em.Should().Be(12);
        ed.Should().Be(30);

        // قالب‌بندی تاریخ شمسی
        var formatted = _calendarService.FormatDate(new DateOnly(2024, 3, 20), FiscalCalendarType.Persian);
        formatted.Should().Be("1403/01/01");
    }

    [Fact]
    public async Task Scenario09_ConcurrentVoucherNumbering_ShouldNotProduceDuplicates()
    {
        using var dbContext = new AccountingDbContext(_options);
        var numberingService = new VoucherNumberingService(dbContext);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();

        // شبیه‌سازی درخواست‌های همزمان ۲۰ کاربر/ترد برای صدور شماره سند در یک سال مالی
        var concurrentTasks = Enumerable.Range(1, 20)
            .Select(async _ =>
            {
                // هر ترد از یک DbContext اختصاصی روی همان دیتابیس مشترک بهره می‌برد
                using var threadContext = new AccountingDbContext(_options);
                var threadNumbering = new VoucherNumberingService(threadContext);
                return await threadNumbering.NextNumberAsync(tenantId, companyId, fiscalYearId, "Posted");
            })
            .ToList();

        var numbers = await Task.WhenAll(concurrentTasks);

        // ارزیابی: ۲۰ شماره تولید شده و همگی باید متمایز و بدون هیچ تکراری باشند
        numbers.Should().HaveCount(20);
        numbers.Distinct().Should().HaveCount(20);
        numbers.Min().Should().Be(1);
        numbers.Max().Should().Be(20);
    }

    [Fact]
    public async Task Scenario10_DuplicateVoucherNumber_ShouldBeBlockedByConstraint()
    {
        using var dbContext = new AccountingDbContext(_options);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();
        var fiscalPeriodId = Guid.NewGuid();

        // سند اول با شماره 1001
        var doc1 = new AccountingDocument(
            tenantId,
            companyId,
            fiscalYearId,
            fiscalPeriodId,
            documentNumber: 1001,
            documentDate: new DateOnly(2024, 5, 1),
            description: "سند اول");

        dbContext.Documents.Add(doc1);
        await dbContext.SaveChangesAsync();

        // سند دوم با همان شماره 1001 در همان سال مالی و شرکت
        var doc2 = new AccountingDocument(
            tenantId,
            companyId,
            fiscalYearId,
            fiscalPeriodId,
            documentNumber: 1001, // شماره تکراری!
            documentDate: new DateOnly(2024, 5, 2),
            description: "سند دوم متداخل");

        dbContext.Documents.Add(doc2);

        // ایندکس ترکیبی یکتا مانع ثبت شماره تکراری در پایگاه داده می‌شود
        var saveAction = async () => await dbContext.SaveChangesAsync();
        await saveAction.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Scenario11_SettingsChange_ShouldNotAffectExistingDocuments()
    {
        using var dbContext = new AccountingDbContext(_options);
        var settingsService = new AccountingSettingsService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // 1. دریافت و تنظیم اولیه
        var initialSettings = await settingsService.GetSettingsAsync(tenantId, companyId);
        initialSettings.DefaultCurrencyCode.Should().Be("IRR");
        initialSettings.DecimalPlaces.Should().Be(0);

        // 2. ثبت یک سند مالی بر اساس تنظیمات فعلی
        var doc = new AccountingDocument(
            tenantId,
            companyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            documentNumber: 5001,
            documentDate: new DateOnly(2024, 6, 1),
            description: "سند قطعی قبلی")
        {
            CurrencyCode = initialSettings.DefaultCurrencyCode
        };

        dbContext.Documents.Add(doc);
        await dbContext.SaveChangesAsync();

        // 3. ویرایش تنظیمات حسابداری شرکت (مثلاً تغییر به واحد پول USD، اعشار ۲، و تقویم میلادی)
        var updateRequest = new UpdateAccountingSettingsRequest(
            DefaultCurrencyCode: "USD",
            CalendarType: FiscalCalendarType.Gregorian,
            FiscalYearStartMonth: 1,
            FiscalYearStartDay: 1,
            DecimalPlaces: 2,
            RoundingMode: RoundingMode.AwayFromZero,
            VoucherNumberingPolicy: VoucherNumberingPolicy.MonthlyRestart,
            AllowNegativeBalancesPolicy: AllowNegativeBalancesPolicy.StrictlyForbidden,
            ApprovalPolicy: DocumentApprovalPolicy.TwoStepApprovalRequired,
            DefaultAccountTemplateId: null,
            LockDate: new DateOnly(2024, 6, 30),
            AllowUnbalancedDraft: false,
            AutoNumberingEnabled: true,
            ChangeReason: "تغییر واحد پول عملیاتی شرکت به دلار بر اساس مصوبه هیئت مدیره");

        var updatedSettings = await settingsService.UpdateSettingsAsync(tenantId, companyId, updateRequest, updatedBy: "cfo_user");

        // 4. اعتبارسنجی: تاریخچه سابقه قبلی به درستی ثبت شده است
        var history = await settingsService.GetSettingsHistoryAsync(tenantId, companyId);
        history.Should().ContainSingle();
        history[0].DefaultCurrencyCode.Should().Be("IRR");
        history[0].ChangeReason.Should().Contain("مصوبه هیئت مدیره");

        // 5. اعتبارسنجی: سند قبلی صادرشده کاملاً دست‌نخورده باقی مانده است
        var loadedDoc = await dbContext.Documents.FirstAsync(d => d.Id == doc.Id);
        loadedDoc.CurrencyCode.Should().Be("IRR");
        loadedDoc.DocumentNumber.Should().Be(5001);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
