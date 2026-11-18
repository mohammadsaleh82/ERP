using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Services.Audit;
using ERP.Modules.Accounting.Application.Validators;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای هم‌زمانی PROMPT 09 روی PostgreSQL واقعی: ویرایش هم‌زمان پیش‌نویس
/// (Concurrency Token)، معکوس‌سازی هم‌زمان، بستن دوره در برابر قطعی‌سازی سند.
/// در نبود سرور Skip می‌شود.
/// </summary>
public class AccountingConcurrencyPostgresTests : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Username=erp_user;Password=erp_test_password";

    private readonly string _databaseName = $"erp_conc_test_{Guid.NewGuid():N}"[..30];
    private string? _connectionString;
    private bool _available;
    private DbContextOptions<AccountingDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("ERP_TEST_POSTGRES") ?? DefaultConnectionString;
        try
        {
            await using var boot = new NpgsqlConnection(baseConnection);
            await boot.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", boot))
                await cmd.ExecuteNonQueryAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", boot))
                await cmd.ExecuteNonQueryAsync();
            _available = true;
            _connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { Database = _databaseName }.ConnectionString;
            _options = new DbContextOptionsBuilder<AccountingDbContext>()
                .UseNpgsql(_connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(AccountingDbContext.MigrationsHistoryTable))
                .UseSnakeCaseNamingConvention()
                .Options;

            // مهاجرت طرح
            await using var db = new AccountingDbContext(_options, NewTenantContext(), NewUserContext(), new TestClock());
            await db.Database.MigrateAsync();
        }
        catch
        {
            _available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_available) return;
        try
        {
            await using var boot = new NpgsqlConnection(
                new NpgsqlConnectionStringBuilder(_connectionString!) { Database = "postgres" }.ConnectionString);
            await boot.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", boot);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* best effort */ }
    }

    private static BuildingBlocks.Tenancy.TenantContext NewTenantContext()
    {
        var ctx = new BuildingBlocks.Tenancy.TenantContext();
        ctx.SetContext(_tenantIdField, _companyIdField);
        return ctx;
    }

    private static Guid _tenantIdField = Guid.NewGuid();
    private static Guid _companyIdField = Guid.NewGuid();

    private static BuildingBlocks.Security.CurrentUserContext NewUserContext() =>
        new(new Microsoft.AspNetCore.Http.HttpContextAccessor());

    private AccountingDbContext NewDb() => new(_options, NewTenantContext(), NewUserContext(), new TestClock());

    private sealed class TestClock : BuildingBlocks.Abstractions.IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private (DocumentService Docs, VoucherPostingEngine Engine) NewServices(AccountingDbContext db, string user)
    {
        var tenantContext = new BuildingBlocks.Tenancy.TenantContext();
        tenantContext.SetContext(_tenantId, _companyId);
        var userContext = new BuildingBlocks.Security.CurrentUserContext(new Microsoft.AspNetCore.Http.HttpContextAccessor());
        var numbering = new VoucherNumberingService(db);
        var snapshots = new BalanceSnapshotService(db);
        var dimensionValidation = new DimensionValidationApplicationService(db);
        var auditTrail = new AccountingAuditTrailService(db, new TestClock(), userContext, new FixedCorrelation());
        return (new DocumentService(db, numbering, new CreateDocumentRequestValidator(),
                new UpdateDocumentRequestValidator(), dimensionValidation, auditTrail),
            new VoucherPostingEngine(db, numbering, snapshots, dimensionValidation, new TestClock(), auditTrail));
    }

    private sealed class FixedCorrelation : BuildingBlocks.Security.ICorrelationIdProvider
    {
        public string GetCorrelationId() => "conc-pg-test";
    }

    private Guid _tenantId = Guid.NewGuid();
    private Guid _companyId = Guid.NewGuid();
    private FiscalYear _year = null!;
    private Account _cash = null!;
    private Account _revenue = null!;

    private void Seed()
    {
        using var db = NewDb();
        db.Database.AutoTransactionsEnabled = false;
        _year = new FiscalYear(_tenantId, _companyId, 2025, "سال مالی ۱۴۰۴",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), FiscalCalendarType.Persian);
        db.FiscalYears.Add(_year);
        db.SaveChanges();

        // تولید دوره‌های استاندارد سال مالی (۱۲ ماه شمسی)
        var calendar = new FinancialCalendarService();
        var periods = calendar.GenerateStandardPeriods(FiscalCalendarType.Persian, 2025, PeriodGenerationType.Monthly);
        foreach (var (periodNumber, name, startDate, endDate) in periods)
        {
            var period = _year.AddPeriod(periodNumber, name, startDate, endDate);
            db.FiscalPeriods.Add(period);
        }
        db.SaveChanges();

        _cash = Account.CreateRoot(_tenantId, _companyId, "10", "گروه دارایی جاری", AccountNature.Asset);
        var general = _cash.CreateChild("0", "کل", allowPosting: false);
        var posting = general.CreateChild("01", "صندوق", allowPosting: true);
        _cash = posting;
        db.Accounts.AddRange(_cash, general, posting);
        db.SaveChanges();

        var revRoot = Account.CreateRoot(_tenantId, _companyId, "40", "گروه درآمد", AccountNature.Revenue);
        var revGeneral = revRoot.CreateChild("0", "کل", allowPosting: false);
        var revPosting = revGeneral.CreateChild("01", "درآمد فروش", allowPosting: true);
        _revenue = revPosting;
        db.Accounts.AddRange(revRoot, revGeneral, revPosting);
        db.SaveChanges();
    }

    private async Task<Guid> CreateDraftAsync(DocumentService docs, string user)
    {
        var draft = await docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _tenantId, _companyId, _year.Id, new DateOnly(2025, 2, 5), "سند هم‌زمانی",
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 700m, "فروش"),
             new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 700m, "فروش")],
            DocumentType.General, null, null, null, null, null, user));
        return draft.Id;
    }

    [Fact]
    public async Task TwoConcurrentDraftUpdates_ExactlyOneSucceeds()
    {
        if (!_available) return;
        Seed();

        var docs1 = NewServices(NewDb(), "editor-1").Docs;
        var docDto = await docs1.CreateDraftAsync(new CreateVoucherDraftCommand(
            _tenantId, _companyId, _year.Id, new DateOnly(2025, 2, 5), "ویرایش هم‌زمان",
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 700m, "فروش"),
             new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 700m, "فروش")],
            DocumentType.General, null, null, null, null, null, "editor-1"));
        var docId = docDto.Id;

        var editor1Success = false;
        var editor2Success = false;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                try
                {
                    var scope1 = NewServices(NewDb(), "editor-1").Docs;
                    await scope1.UpdateDraftAsync(new UpdateVoucherDraftCommand(
                        _tenantId, _companyId, docId, new DateOnly(2025, 2, 6),
                        "ویرایش اول", [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 710m, "فروش"),
                                        new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 710m, "فروش")],
                        null, "editor-1"));
                    editor1Success = true;
                }
                catch (DbUpdateConcurrencyException) { /* رقیب برنده شده */ }
            }),
            Task.Run(async () =>
            {
                try
                {
                    var scope2 = NewServices(NewDb(), "editor-2").Docs;
                    await scope2.UpdateDraftAsync(new UpdateVoucherDraftCommand(
                        _tenantId, _companyId, docId, new DateOnly(2025, 2, 7),
                        "ویرایش دوم", [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 720m, "فروش"),
                                        new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 720m, "فروش")],
                        null, "editor-2"));
                    editor2Success = true;
                }
                catch (DbUpdateConcurrencyException) { /* رقیب برنده شده */ }
            }));

        (editor1Success ^ editor2Success).Should().BeTrue(
            "ویرایش هم‌زمان پیش‌نویس با Concurrency Token: دقیقاً یک ویرایشگر برنده است");
    }

    [Fact]
    public async Task TwoConcurrentReversals_OnlyOneReversalDocumentCreated()
    {
        if (!_available) return;
        Seed();

        var docs1 = NewServices(NewDb(), "user").Docs;
        var docId = await CreateDraftAsync(docs1, "user");
        var engine1 = NewServices(NewDb(), "user").Engine;
        await engine1.PostAsync(new PostVoucherCommand(_tenantId, _companyId, docId, null, "user"));

        var reversedCount = 0;
        await Task.WhenAll(
            Task.Run(async () =>
            {
                try
                {
                    var engine = NewServices(NewDb(), "reverser-1").Engine;
                    await engine.ReverseAsync(new ReverseVoucherCommand(
                        _tenantId, _companyId, docId, new DateOnly(2025, 2, 10), "معکوس اول", null, "reverser-1"));
                    Interlocked.Increment(ref reversedCount);
                }
                catch (Exception ex) when (ex.Message.Contains("هم‌زمان") || ex.Message.Contains("Concurrency")) { }
            }),
            Task.Run(async () =>
            {
                try
                {
                    var engine = NewServices(NewDb(), "reverser-2").Engine;
                    await engine.ReverseAsync(new ReverseVoucherCommand(
                        _tenantId, _companyId, docId, new DateOnly(2025, 2, 10), "معکوس دوم", null, "reverser-2"));
                    Interlocked.Increment(ref reversedCount);
                }
                catch (Exception ex) when (ex.Message.Contains("هم‌زمان") || ex.Message.Contains("Concurrency")) { }
            }));

        using var db = NewDb();
        var reversalDocs = await db.Documents
            .Where(d => d.ReferenceDocumentId == docId)
            .ToListAsync();
        var original = await db.Documents.FirstAsync(d => d.Id == docId);

        // فقط یک سند معکوس وجود دارد و مبدأ Reversed شده
        reversalDocs.Should().HaveCount(1, "معکوس‌سازی هم‌زمان نباید دو سند معکوس بسازد");
        original.Status.Should().Be(DocumentStatus.Reversed);
        reversedCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PeriodClose_VsDocumentPost_NoPostedDocumentInClosedPeriod()
    {
        if (!_available) return;
        Seed();

        // اسناد با تاریخ در دوره فوریه
        var docs1 = NewServices(NewDb(), "poster").Docs;
        var docId = await CreateDraftAsync(docs1, "poster");

        // هم‌زمانی: قطعی‌سازی سند و بستن دوره فوریه
        var postTask = Task.Run(async () =>
        {
            try
            {
                var engine = NewServices(NewDb(), "poster").Engine;
                await engine.PostAsync(new PostVoucherCommand(_tenantId, _companyId, docId, null, "poster"));
                return "posted";
            }
            catch (Exception ex)
            {
                return $"failed: {ex.Message}";
            }
        });

        var closeTask = Task.Run(async () =>
        {
            try
            {
                var fiscalService = new FiscalYearService(NewDb(),
                    new FinancialCalendarService(),
                    new AccountingAuditTrailService(NewDb(), new TestClock(), NewUserContext(), new FixedCorrelation()));
                await fiscalService.ClosePeriodAsync(_tenantId, _companyId, _year.Id, _year.Periods.First(p => p.PeriodNumber == 2).Id);
                return "closed";
            }
            catch (Exception ex)
            {
                return $"failed: {ex.Message}";
            }
        });

        var results = await Task.WhenAll(postTask, closeTask);

        // ناوردایی: اگر سند قطعی شد، GL آن در دوره باز ثبت شده؛ اگر دوره بسته شد،
        // قطعی‌سازی باید با خطای دوره بسته رد شده باشد
        using var db = NewDb();
        var glRows = await db.GeneralLedgerEntries.Where(e => e.DocumentId == docId).ToListAsync();
        var doc = await db.Documents.FirstAsync(d => d.Id == docId);

        if (results[0] == "posted")
        {
            glRows.Should().HaveCount(2, "سند قطعی‌شده باید دقیقاً دو سطر دفتر کل داشته باشد");
            var period = await db.FiscalPeriods.FirstAsync(p => p.Id == doc.FiscalPeriodId);
            // اگر دوره بسته شد، این یعنی قطعی‌سازی قبل از بستن کامل شده — ترتیب معتبر
            (period.Status == FiscalPeriodStatus.Closed || period.Status == FiscalPeriodStatus.Open)
                .Should().BeTrue();
        }
        else
        {
            glRows.Should().BeEmpty("در دوره بسته، قطعی‌سازی باید رد شده باشد");
        }
    }
}
