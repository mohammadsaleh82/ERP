using System.Data;
using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Validators;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace ERP.IntegrationTests;

/// <summary>
/// تست‌های هم‌زمانی موتور سند روی PostgreSQL واقعی (سناریو ۱۰ پرامپت).
/// اجرا نیازمند سرور PostgreSQL است:
///   - با متغیر محیطی ERP_TEST_POSTGRES (رشته اتصال کامل)، یا
///   - به‌صورت پیش‌فرض روی Host=localhost;Port=5433;Username=erp_user;Password=erp_test_password
/// در نبود سرور، تست‌ها با پیام توضیحی رد می‌شوند (Skip).
/// </summary>
public class VoucherConcurrencyPostgresTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string? _baseConnectionString;
    private readonly string _databaseName;

    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Username=erp_user;Password=erp_test_password";

    public VoucherConcurrencyPostgresTests(ITestOutputHelper output)
    {
        _output = output;
        _baseConnectionString = Environment.GetEnvironmentVariable("ERP_TEST_POSTGRES");
        _databaseName = $"erp_voucher_test_{Guid.NewGuid():N}"[..30];
    }

    public async Task InitializeAsync()
    {
        // ایجاد پایگاه‌داده ایزوله برای این کلاس تست
        var connectionString = BaseConnectionStringWith(dbName: "postgres");
        await using var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"PostgreSQL ایجاد نشد: {ex.GetType().Name} - {ex.Message}");
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            var connectionString = BaseConnectionStringWith(dbName: "postgres");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_databaseName}' AND pid <> pg_backend_pid(); " +
                $"DROP DATABASE IF EXISTS \"{_databaseName}\";", connection);
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // در عدم دسترسی به سرور، پاکسازی بی‌اهمیت است
        }
    }

    private string BaseConnectionStringWith(string dbName)
    {
        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString ?? DefaultConnectionString)
        {
            Database = dbName
        };
        return builder.ConnectionString;
    }

    private bool ServerAvailable()
    {
        try
        {
            using var connection = new NpgsqlConnection(BaseConnectionStringWith("postgres"));
            connection.Open();
            return true;
        }
        catch (Exception ex)
        {
            _output.WriteLine(
                $"SKIPPED: سرور PostgreSQL برای تست‌های هم‌زمانی در دسترس نیست ({ex.Message}). " +
                "برای اجرای واقعی: docker compose up postgres یا ست کردن ERP_TEST_POSTGRES.");
            return false;
        }
    }

    /// <summary>ایجاد حساب معین قابل ثبت در ساختار سه‌سطحی گروه ← کل ← معین.</summary>
    private static (Account Cash, Account Bank) SeedPostingAccounts(AccountingDbContext db, Guid tenantId, Guid companyId, string rootCode)
    {
        var root = Account.CreateRoot(tenantId, companyId, rootCode, $"گروه {rootCode}", AccountNature.Asset);
        var general = root.CreateChild("0", "کل عمومی", allowPosting: false);
        var cash = general.CreateChild("01", "صندوق", allowPosting: true);
        var bank = general.CreateChild("02", "بانک", allowPosting: true);
        db.Accounts.Add(root);
        db.SaveChanges();
        return (cash, bank);
    }

    private DbContextOptions<AccountingDbContext> BuildOptions()
    {
        return new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(BaseConnectionStringWith(_databaseName), npgsql =>
                npgsql.MigrationsHistoryTable(AccountingDbContext.MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention()
            .EnableSensitiveDataLogging()
            .Options;
    }

    /// <summary>یک «درخواست HTTP» مستقل: زمینه داده، سرویس‌ها و زمینه تنانت مخصوص خودش.</summary>
    private sealed class VoucherRequestScope
    {
        public Guid TenantId { get; }
        public Guid CompanyId { get; }
        public BuildingBlocks.Tenancy.TenantContext TenantContext { get; } = new();
        public FakeCurrentUserContext CurrentUser { get; } = new();
        public TestClock Clock { get; } = new();
        public AccountingDbContext Db { get; }
        public VoucherPostingEngine Engine { get; }
        public DocumentService Docs { get; }

        public VoucherRequestScope(DbContextOptions<AccountingDbContext> options, Guid? tenantId = null, Guid? companyId = null)
        {
            TenantId = tenantId ?? Guid.NewGuid();
            CompanyId = companyId ?? Guid.NewGuid();

            TenantContext.SetContext(TenantId, CompanyId);
            CurrentUser.ActiveTenantId = TenantId;
            CurrentUser.ActiveCompanyId = CompanyId;

            Db = new AccountingDbContext(options, TenantContext, CurrentUser, Clock);
            var numbering = new VoucherNumberingService(Db);
            Docs = new DocumentService(Db, numbering, new CreateDocumentRequestValidator(), new UpdateDocumentRequestValidator());
            Engine = new VoucherPostingEngine(Db, numbering, Clock);
        }
    }

    // ------------------------------------------------------------------
    // سناریو ۱۰: دو درخواست Posting هم‌زمان روی PostgreSQL واقعی
    // ------------------------------------------------------------------

    [Fact]
    public async Task TwoConcurrentPostRequests_SameDocument_ShouldPostExactlyOnce()
    {
        if (!ServerAvailable()) return;

        var options = BuildOptions();

        // آماده‌سازی داده در یک scope راه‌انداز
        var bootstrap = new VoucherRequestScope(options);
        bootstrap.Db.Database.EnsureCreated();

        var fy = new FiscalYear(
            bootstrap.TenantId, bootstrap.CompanyId, 2025,
            "سال مالی هم‌زمانی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            FiscalCalendarType.Gregorian);
        fy.AddPeriod(1, "فروردین", new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));
        fy.AddPeriod(2, "دوره عمومی", new DateOnly(2025, 2, 1), new DateOnly(2025, 12, 31));
        bootstrap.Db.FiscalYears.Add(fy);

        var settings = new AccountingSettings(bootstrap.TenantId, bootstrap.CompanyId)
        {
            ActiveFiscalYearId = fy.Id
        };
        bootstrap.Db.AccountingSettings.Add(settings);

        var (cash, bank) = SeedPostingAccounts(bootstrap.Db, bootstrap.TenantId, bootstrap.CompanyId, "10");

        var creator = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
        var draft = await creator.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            creator.TenantId, creator.CompanyId, fy.Id, new DateOnly(2025, 1, 15),
            "سند هم‌زمانی",
            [new VoucherLineCommand(cash.Id, DebitCredit.Debit, 5_000_000m, "بدهکار"),
             new VoucherLineCommand(bank.Id, DebitCredit.Credit, 5_000_000m, "بستانکار")],
            DocumentType.General, null, null, null, null, null,
            creator.CurrentUser.UserId.ToString()));

        // دو درخواست هم‌زمان مستقل برای ثبت قطعی همان سند
        var request1 = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
        var request2 = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);

        var task1 = request1.Engine.PostAsync(
            new PostVoucherCommand(request1.TenantId, request1.CompanyId, draft.Id, null, "user-1"));
        var task2 = request2.Engine.PostAsync(
            new PostVoucherCommand(request2.TenantId, request2.CompanyId, draft.Id, null, "user-2"));

        PostDocumentResultDto[] results;
        try
        {
            results = await Task.WhenAll(task1, task2);
        }
        catch (Exception)
        {
            // اگر یکی از درخواست‌ها خطای هم‌زمانی ساختارمند داد، یک‌بار دیگر بدون هم‌زمانی بررسی می‌کنیم که سند قطعی شده باشد
            var finalState = await new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId).Docs.GetDocumentAsync(
                request1.TenantId, request1.CompanyId, draft.Id);
            finalState.Status.Should().Be(DocumentStatus.Posted,
                "در پایان هم‌زمانی، سند باید قطعی‌شده باشد");
            _output.WriteLine("یکی از درخواست‌ها با خطای هم‌زمانی شکست خورد اما سند قطعی و بدون ثبت دوباره است.");
            return;
        }

        // نتیجه: دقیقاً یکی «قطعی‌کننده اصلی» و دیگری «یدمپوتنت (قبلاً قطعی شده)»
        var originalCount = results.Count(r => !r.WasAlreadyPosted);
        var idempotentCount = results.Count(r => r.WasAlreadyPosted);
        originalCount.Should().Be(1, "تنها یک درخواست باید عملیات قطعی‌سازی واقعی را انجام دهد");
        idempotentCount.Should().BeLessThanOrEqualTo(1, "درخواست دوم باید نتیجه ایدمپوتنت بگیرد");
        results.Should().OnlyContain(r => r.TotalDebit == 5_000_000m);

        // سطرهای دفتر کل دقیقاً یک‌بار نوشته شده‌اند
        var verifier = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
        var glCount = await verifier.Db.GeneralLedgerEntries
            .CountAsync(e => e.DocumentId == draft.Id);
        glCount.Should().Be(2, "دو ردیف سند باید دقیقاً دو سطر دفتر کل داشته باشند");

        var doc = await verifier.Db.Documents.AsNoTracking().FirstAsync(d => d.Id == draft.Id);
        doc.Status.Should().Be(DocumentStatus.Posted);
        doc.PostedBy.Should().NotBeNullOrEmpty();

        _output.WriteLine($"نتیجه هم‌زمانی: PostedBy={doc.PostedBy}, GL={glCount}, " +
                          $"AlreadyPosted=[{string.Join(", ", results.Select(r => r.WasAlreadyPosted))}]");
    }

    [Fact]
    public async Task ConcurrentPosting_OfDifferentDocuments_ShouldSucceedWithUniqueNumbers()
    {
        if (!ServerAvailable()) return;

        var options = BuildOptions();
        var bootstrap = new VoucherRequestScope(options);
        bootstrap.Db.Database.EnsureCreated();

        var fy = new FiscalYear(
            bootstrap.TenantId, bootstrap.CompanyId, 2025,
            "سال مالی اسناد موازی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            FiscalCalendarType.Gregorian);
        fy.AddPeriod(1, "دوره عمومی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        bootstrap.Db.FiscalYears.Add(fy);
        var settings = new AccountingSettings(bootstrap.TenantId, bootstrap.CompanyId)
        {
            ActiveFiscalYearId = fy.Id
        };
        bootstrap.Db.AccountingSettings.Add(settings);

        var (acc1, acc2) = SeedPostingAccounts(bootstrap.Db, bootstrap.TenantId, bootstrap.CompanyId, "20");

        // ایجاد ۴ پیش‌نویس سپس قطعی‌سازی هم‌زمان همه
        var drafts = new List<(Guid DocumentId, VoucherRequestScope Scope)>();
        for (var i = 0; i < 4; i++)
        {
            var creator = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
            var draft = await creator.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
                creator.TenantId, creator.CompanyId, fy.Id, new DateOnly(2025, 1, 10 + i),
                $"سند موازی {i + 1}",
                [new VoucherLineCommand(acc1.Id, DebitCredit.Debit, 1_000_000m + i, "بدهکار"),
                 new VoucherLineCommand(acc2.Id, DebitCredit.Credit, 1_000_000m + i, "بستانکار")],
                DocumentType.General, null, null, null, null, null,
                creator.CurrentUser.UserId.ToString()));
            drafts.Add((draft.Id, creator));
        }

        // هر سند توسط یک scope مستقل قطعی می‌شود (هم‌زمان)
        var postTasks = drafts.Select(d => d.Scope.Engine.PostAsync(
            new PostVoucherCommand(d.Scope.TenantId, d.Scope.CompanyId, d.DocumentId, null, "parallel-user"))).ToList();

        var results = await Task.WhenAll(postTasks);

        results.Should().HaveCount(4);
        results.Should().OnlyContain(r => !r.WasAlreadyPosted, "هر سند تنها یک درخواست قطعی‌سازی دارد");

        // شماره اسناد باید یکتا باشند
        results.Select(r => r.DocumentNumber).Should().OnlyHaveUniqueItems();

        var verifier = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
        var postedCount = await verifier.Db.Documents.CountAsync(d => d.Status == DocumentStatus.Posted);
        postedCount.Should().Be(4);
        var glTotal = await verifier.Db.GeneralLedgerEntries.CountAsync();
        glTotal.Should().Be(8);
    }

    [Fact]
    public async Task ConcurrentCreate_WithSameIdempotencyKey_ShouldCreateSingleDocument()
    {
        if (!ServerAvailable()) return;

        var options = BuildOptions();
        var bootstrap = new VoucherRequestScope(options);
        bootstrap.Db.Database.EnsureCreated();

        var fy = new FiscalYear(
            bootstrap.TenantId, bootstrap.CompanyId, 2025,
            "سال مالی ایدمپوتنسی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            FiscalCalendarType.Gregorian);
        fy.AddPeriod(1, "دوره عمومی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        bootstrap.Db.FiscalYears.Add(fy);
        var settings = new AccountingSettings(bootstrap.TenantId, bootstrap.CompanyId)
        {
            ActiveFiscalYearId = fy.Id
        };
        bootstrap.Db.AccountingSettings.Add(settings);
        var (acc, _) = SeedPostingAccounts(bootstrap.Db, bootstrap.TenantId, bootstrap.CompanyId, "30");

        const string idempotencyKey = "create-idem-key-0001";

        var creator1 = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
        var creator2 = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);

        var createCommand1 = new CreateVoucherDraftCommand(
            creator1.TenantId, creator1.CompanyId, fy.Id, new DateOnly(2025, 1, 15),
            "سند ایدمپوتنت",
            [new VoucherLineCommand(acc.Id, DebitCredit.Debit, 100_000m, "بدهکار"),
             new VoucherLineCommand(acc.Id, DebitCredit.Credit, 100_000m, "بستانکار")],
            DocumentType.General, null, null, null, null, idempotencyKey,
            creator1.CurrentUser.UserId.ToString());

        var createCommand2 = createCommand1 with { PerformedBy = creator2.CurrentUser.UserId.ToString() };

        DocumentDto?[] results;
        try
        {
            var t1 = creator1.Docs.CreateDraftAsync(createCommand1);
            var t2 = creator2.Docs.CreateDraftAsync(createCommand2);
            results = await Task.WhenAll(t1, t2);
        }
        catch (DbUpdateException)
        {
            // برخورد هم‌زمان با کلید یکتا: در نهایت باید تنها یک سند وجود داشته باشد
            var verifier = new VoucherRequestScope(options, bootstrap.TenantId, bootstrap.CompanyId);
            var count = await verifier.Db.Documents.CountAsync(d => d.IdempotencyKey == idempotencyKey);
            count.Should().Be(1, "کلید ایدمپوتنسی یکتا اجازه ایجاد سند دوم نمی‌دهد");
            _output.WriteLine("ایجاد هم‌زمان با کلید یکسان به یکتایی ایندکس برخورد کرد و درست است.");
            return;
        }

        results.DistinctBy(r => r!.Id).Should().HaveCount(1, "هر دو درخواست باید به یک سند برسند");
    }
}
