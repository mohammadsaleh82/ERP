using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Validators;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>شبیه‌سازی کاربر جاری برای تست‌ها.</summary>
public sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string? Email { get; set; } = "tester@erp.local";
    public bool IsAuthenticated { get; set; } = true;
    public Guid? ActiveTenantId { get; set; }
    public Guid? ActiveCompanyId { get; set; }
    public Guid? ActiveBranchId { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = ["Admin"];
    public IReadOnlyList<string> Permissions { get; set; } = [];

    Guid? ICurrentUserContext.UserId => UserId;
    string? ICurrentUserContext.Email => Email;
    bool ICurrentUserContext.IsAuthenticated => IsAuthenticated;
    Guid? ICurrentUserContext.ActiveTenantId => ActiveTenantId;
    Guid? ICurrentUserContext.ActiveCompanyId => ActiveCompanyId;
    Guid? ICurrentUserContext.ActiveBranchId => ActiveBranchId;
    IReadOnlyList<string> ICurrentUserContext.Roles => Roles;
    IReadOnlyList<string> ICurrentUserContext.Permissions => Permissions;
    bool ICurrentUserContext.HasPermission(string permission) => Roles.Contains("Admin") || Permissions.Contains(permission);
    bool ICurrentUserContext.IsInRole(string role) => Roles.Contains(role);
}

/// <summary>ساعت کنترل‌شده برای تست‌ها.</summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// بستر تست موتور سند: پایگاه‌داده SQLite درون‌حافظه‌ای با سرویس‌های واقعی ماژول حسابداری
/// (DocumentService، VoucherPostingEngine، VoucherNumberingService).
/// </summary>
public sealed class VoucherTestHarness : IDisposable
{
    public Guid TenantId { get; init; } = Guid.NewGuid();
    public Guid CompanyId { get; init; } = Guid.NewGuid();

    public SqliteConnection Connection { get; }
    public BuildingBlocks.Tenancy.TenantContext TenantContext { get; } = new();
    public FakeCurrentUserContext CurrentUser { get; } = new();
    public TestClock Clock { get; } = new();
    public DbContextOptions<AccountingDbContext> Options { get; }

    private readonly bool _ownsConnection;

    public VoucherTestHarness(SqliteConnection? sharedConnection = null)
    {
        _ownsConnection = sharedConnection is null;
        Connection = sharedConnection ?? new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        Options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlite(Connection)
            .Options;

        if (sharedConnection is null)
        {
            using var init = NewDbContext();
            init.Database.EnsureCreated();
        }
    }

    public static VoucherTestHarness Create(Guid tenantId, Guid companyId, SqliteConnection? shared = null)
    {
        var harness = new VoucherTestHarness(shared) { TenantId = tenantId, CompanyId = companyId };
        harness.Act(tenantId, companyId);
        return harness;
    }

    public void Act(Guid tenantId, Guid companyId)
    {
        TenantContext.SetContext(tenantId, companyId);
        CurrentUser.ActiveTenantId = tenantId;
        CurrentUser.ActiveCompanyId = companyId;
    }

    public AccountingDbContext NewDbContext() =>
        new(Options, TenantContext, CurrentUser, Clock);

    /// <summary>ایجاد سرویس‌های تازه روی یک زمینه داده تازه (شبیه‌سازی هر درخواست HTTP).</summary>
    public (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) NewServices()
    {
        var db = NewDbContext();
        var numbering = new VoucherNumberingService(db);
        var snapshots = new BalanceSnapshotService(db);
        var dimensionValidation = new DimensionValidationApplicationService(db);
        var docs = new DocumentService(db, numbering, new CreateDocumentRequestValidator(), new UpdateDocumentRequestValidator(), dimensionValidation);
        var engine = new VoucherPostingEngine(db, numbering, snapshots, dimensionValidation, Clock);
        return (docs, engine, db);
    }

    // ------------------------------------------------------------------
    // Seed helpers
    // ------------------------------------------------------------------

    public FiscalYear SeedOpenFiscalYear(int startYear = 2025, bool open = true)
    {
        var fy = new FiscalYear(
            TenantId, CompanyId, startYear,
            $"سال مالی {startYear}",
            new DateOnly(startYear, 1, 1),
            new DateOnly(startYear, 12, 31),
            FiscalCalendarType.Gregorian,
            open ? FiscalYearStatus.Open : FiscalYearStatus.Closed);

        fy.AddPeriod(1, "فروردین", new DateOnly(startYear, 1, 1), new DateOnly(startYear, 1, 31));
        fy.AddPeriod(2, "اردیبهشت", new DateOnly(startYear, 2, 1), new DateOnly(startYear, 2, 28));
        fy.AddPeriod(3, "مرداد", new DateOnly(startYear, 3, 1), new DateOnly(startYear, 3, 31));
        fy.AddPeriod(4, "دوره عمومی", new DateOnly(startYear, 4, 1), new DateOnly(startYear, 12, 31));

        using var db = NewDbContext();
        db.FiscalYears.Add(fy);
        db.SaveChanges();
        return fy;
    }

    public AccountingSettings SeedSettings(DocumentApprovalPolicy policy = DocumentApprovalPolicy.DirectPostingAllowed, Guid? activeFiscalYearId = null)
    {
        var settings = new AccountingSettings(TenantId, CompanyId)
        {
            ApprovalPolicy = policy,
            ActiveFiscalYearId = activeFiscalYearId
        };

        using var db = NewDbContext();
        db.AccountingSettings.Add(settings);
        db.SaveChanges();
        return settings;
    }

    public Account SeedPostingAccount(string code, string name, bool isActive = true, Guid? companyId = null, AccountNature nature = AccountNature.Asset)
    {
        var targetCompanyId = companyId ?? CompanyId;
        var rootCode = code[..2]; // گروه: «۱۰»
        var tail = code[2..];     // معین: «۰۱»

        // برای حساب شرکت دیگر، زمینه باید بدون شرکت فعال باشد تا گارد جهش داده نگیرد
        var seedTenantContext = new BuildingBlocks.Tenancy.TenantContext();
        seedTenantContext.SetContext(TenantId, targetCompanyId == CompanyId ? CompanyId : null);

        using var db = new AccountingDbContext(Options, seedTenantContext, null, Clock);

        // ساختار سه‌سطحی: گروه → کل → معین (فقط معین و تفصیلی قابل ثبت سند هستند)
        var root = db.Accounts
            .FirstOrDefault(a => a.Code == rootCode && a.Level == AccountLevel.Group && a.CompanyId == targetCompanyId);

        if (root is null)
        {
            root = Account.CreateRoot(TenantId, targetCompanyId, rootCode, $"گروه {rootCode}", nature);
            db.Accounts.Add(root);
            db.SaveChanges();
        }

        var general = db.Accounts
            .FirstOrDefault(a => a.ParentAccountId == root.Id && a.Code == "0");

        if (general is null)
        {
            general = root.CreateChild("0", "کل عمومی", allowPosting: false);
            db.Accounts.Add(general);
            db.SaveChanges();
        }

        var posting = general.CreateChild(tail, name, allowPosting: true);

        if (!isActive)
        {
            posting.Deactivate();
        }

        db.Accounts.Add(posting);
        db.SaveChanges();
        return posting;
    }

    public async Task<AccountingDocument> SeedBalancedDraftAsync(
        (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) services,
        FiscalYear fy,
        Account debitAccount,
        Account creditAccount,
        decimal amount = 1_000_000m,
        DateOnly? documentDate = null,
        string? idempotencyKey = null)
    {
        var date = documentDate ?? new DateOnly(fy.YearNumber, 1, 15);
        var dto = await services.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            TenantId, CompanyId, fy.Id, date,
            "سند پیش‌نویس تست",
            [new VoucherLineCommand(debitAccount.Id, DebitCredit.Debit, amount, "طرف بدهکار"),
             new VoucherLineCommand(creditAccount.Id, DebitCredit.Credit, amount, "طرف بستانکار")],
            DocumentType.General, null, null, null, null, idempotencyKey,
            CurrentUser.UserId.ToString()), CancellationToken.None);

        return await services.Db.Documents
            .Include(d => d.Lines)
            .AsNoTracking()
            .FirstAsync(d => d.Id == dto.Id, CancellationToken.None);
    }

    public async Task<AccountingDocument?> ReloadDocumentAsync(AccountingDbContext db, Guid documentId)
    {
        return await db.Documents
            .Include(d => d.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_ownsConnection)
        {
            Connection.Close();
            Connection.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
