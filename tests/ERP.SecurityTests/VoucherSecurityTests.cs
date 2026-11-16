using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Validators;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.SecurityTests;

/// <summary>
/// تست‌های امنیتی موتور سند حسابداری:
/// گاردهای جهش داده Tenant/Company، جلوگیری از دستکاری شناسه‌ها، یکتایی کلید ایدمپوتنسی،
/// جداسازی سطرهای دفتر کل و ماتریس مجوزهای تفکیک‌شده عملیات حساس اسناد.
/// </summary>
public class VoucherSecurityTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext = new();
    private readonly DbContextOptions<AccountingDbContext> _options;

    public VoucherSecurityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tenantContext.SetContext(_tenantId, _companyId);

        _options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var init = NewDbContext();
        init.Database.EnsureCreated();

        // سال مالی مشترک برای همه تست‌های این کلاس
        var fy = new FiscalYear(_tenantId, _companyId, 2025, "سال مالی امنیتی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), FiscalCalendarType.Gregorian);
        fy.AddPeriod(1, "دوره عمومی", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        init.FiscalYears.Add(fy);
        init.SaveChanges();
    }

    private AccountingDbContext NewDbContext() =>
        new(_options, _tenantContext, null, null);

    // ------------------------------------------------------------------
    // گارد جهش داده Tenant / Company (SaveChanges Guards)
    // ------------------------------------------------------------------

    [Fact]
    public async Task SaveDocument_WithForeignTenantId_ShouldThrowSecurityException()
    {
        using var db = NewDbContext();
        var doc = new AccountingDocument(
            Guid.NewGuid(), // Tenant دیگر
            _companyId, Guid.NewGuid(), Guid.NewGuid(),
            9001, new DateOnly(2025, 1, 15), "سند با Tenant دیگر");

        db.Documents.Add(doc);
        var saveAction = async () => await db.SaveChangesAsync();
        await saveAction.Should().ThrowAsync<System.Security.SecurityException>()
            .WithMessage("*Tenant violation*");
    }

    [Fact]
    public async Task SaveDocument_WithForeignCompanyId_ShouldThrowSecurityException()
    {
        using var db = NewDbContext();
        var doc = new AccountingDocument(
            _tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            9002, new DateOnly(2025, 1, 15), "سند با Company دیگر");

        db.Documents.Add(doc);
        var saveAction = async () => await db.SaveChangesAsync();
        await saveAction.Should().ThrowAsync<System.Security.SecurityException>()
            .WithMessage("*Company violation*");
    }

    [Fact]
    public async Task UpdateDocument_AssigningForeignCompanyId_ShouldThrowSecurityException()
    {
        // استفاده از سال مالی مشترک seed شده در سازنده
        var fy = NewDbContext().FiscalYears.Include(y => y.Periods).AsNoTracking().First();

        Guid documentId;
        using (var seedDb = NewDbContext())
        {
            var doc = new AccountingDocument(
                _tenantId, _companyId, fy.Id, fy.Periods.First().Id,
                9003, new DateOnly(2025, 1, 15), "سند تست دستکاری CompanyId");
            seedDb.Documents.Add(doc);
            await seedDb.SaveChangesAsync();
            documentId = doc.Id;
        }

        // تلاش برای انتقال سند به شرکت دیگر (دستکاری شناسه سازمانی)
        using var attackerDb = NewDbContext();
        var tamperedDoc = await attackerDb.Documents.FirstAsync(d => d.Id == documentId);
        tamperedDoc.CompanyId = Guid.NewGuid();
        tamperedDoc.FiscalYearId = Guid.NewGuid();

        var saveAction = async () => await attackerDb.SaveChangesAsync();
        await saveAction.Should().ThrowAsync<System.Security.SecurityException>();
    }

    // ------------------------------------------------------------------
    // یکتایی کلید ایدمپوتنسی در سطح پایگاه‌داده
    // ------------------------------------------------------------------

    [Fact]
    public async Task IdempotencyKey_DuplicateAcrossDocuments_ShouldBeRejectedByDatabase()
    {
        using var db = NewDbContext();
        var fyId = db.FiscalYears.Include(y => y.Periods).First().Id;
        var periodId = db.FiscalPeriods.First().Id;

        db.Documents.Add(new AccountingDocument(
            _tenantId, _companyId, fyId, periodId,
            9101, new DateOnly(2025, 1, 15), "سند اول با کلید",
            idempotencyKey: "unique-key-001"));
        await db.SaveChangesAsync();

        db.Documents.Add(new AccountingDocument(
            _tenantId, _companyId, fyId, periodId,
            9102, new DateOnly(2025, 1, 15), "سند دوم با همان کلید",
            idempotencyKey: "unique-key-001"));

        var action = async () => await db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>("ایندکس یکتای کلید ایدمپوتنسی اجازه دو سند با کلید یکسان نمی‌دهد");
    }

    [Fact]
    public async Task IdempotencyKey_MultipleDraftsWithoutKey_ShouldBeAllowed()
    {
        using var db = NewDbContext();
        var fyId = db.FiscalYears.Include(y => y.Periods).First().Id;
        var periodId = db.FiscalPeriods.First().Id;

        for (var i = 0; i < 3; i++)
        {
            db.Documents.Add(new AccountingDocument(
                _tenantId, _companyId, fyId, periodId,
                9200 + i, new DateOnly(2025, 1, 15), $"سند بدون کلید {i + 1}"));
        }

        var action = async () => await db.SaveChangesAsync();
        await action.Should().NotThrowAsync("مقادیر NULL کلید ایدمپوتنسی متعدد مجازند");
    }

    // ------------------------------------------------------------------
    // ماتریس مجوزهای تفکیک‌شده اسناد حسابداری
    // ------------------------------------------------------------------

    public static TheoryData<string> RequiredVoucherPermissions => new()
    {
        "accounting:documents:create",
        "accounting:documents:update",
        "accounting:documents:delete",
        "accounting:documents:read",
        "accounting:documents:submit",
        "accounting:documents:approve",
        "accounting:documents:reject",
        "accounting:documents:post",
        "accounting:documents:reverse"
    };

    [Theory]
    [MemberData(nameof(RequiredVoucherPermissions))]
    public async Task PermissionPolicyProvider_ShouldCreatePolicyForEveryVoucherPermission(string permission)
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(permission);

        policy.Should().NotBeNull($"مجوز '{permission}' باید در زمان اجرا به یک Policy تبدیل شود");
        policy!.Requirements.OfType<PermissionRequirement>()
            .Should().Contain(r => r.Permission == permission);
    }

    [Theory]
    [MemberData(nameof(RequiredVoucherPermissions))]
    public async Task PermissionAuthorizationHandler_UserWithoutPermission_ShouldNotSucceed(string permission)
    {
        var userContext = new TestUserContext(permissions: []); // بدون هیچ مجوز سند
        var handler = new PermissionAuthorizationHandler(userContext);
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], CreatePrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse($"کاربر بدون مجوز '{permission}' نباید مجاز شود");
    }

    [Fact]
    public void UnauthenticatedUser_ShouldHaveNoPermissions()
    {
        var userContext = new TestUserContext(isAuthenticated: false);

        userContext.HasPermission("accounting:documents:post").Should().BeFalse();
    }

    [Fact]
    public void AdminRole_ShouldHaveAllVoucherPermissions()
    {
        var userContext = new TestUserContext(roles: ["Admin"]);

        foreach (var permission in RequiredVoucherPermissions.Cast<string>())
        {
            userContext.HasPermission(permission).Should().BeTrue($"نقش Admin مجوز '{permission}' را دارد");
        }
    }

    // ------------------------------------------------------------------
    // جداسازی Tenant در سطح اسناد و سطرهای دفتر کل
    // ------------------------------------------------------------------

    [Fact]
    public async Task GlEntries_OfAnotherTenant_ShouldBeInvisible()
    {
        var fy = NewDbContext().FiscalYears.Include(y => y.Periods).AsNoTracking().First();

        Guid documentId;
        using (var seedDb = NewDbContext())
        {
            // حساب‌های واقعی (FK ردیف‌ها به حساب الزامی است)
            var root = Account.CreateRoot(_tenantId, _companyId, "10", "گروه دارایی", AccountNature.Asset);
            var general = root.CreateChild("0", "کل عمومی", allowPosting: false);
            var cash = general.CreateChild("01", "صندوق", allowPosting: true);
            var bank = general.CreateChild("02", "بانک", allowPosting: true);
            seedDb.Accounts.Add(root);

            var doc = new AccountingDocument(
                _tenantId, _companyId, fy.Id, fy.Periods.First().Id,
                9301, new DateOnly(2025, 1, 15), "سند قطعی برای تست GL");
            doc.AddLine(cash.Id, DebitCredit.Debit, 500_000m, "بدهکار");
            doc.AddLine(bank.Id, DebitCredit.Credit, 500_000m, "بستانکار");
            doc.Post(DocumentApprovalPolicy.DirectPostingAllowed);

            seedDb.Documents.Add(doc);
            foreach (var line in doc.Lines)
            {
                seedDb.GeneralLedgerEntries.Add(GeneralLedgerEntry.FromDocumentLine(
                    doc, line, fy.Periods.First().Id, "user", DateTimeOffset.UtcNow));
            }
            await seedDb.SaveChangesAsync();
            documentId = doc.Id;
        }

        // مستاجر دیگر روی همان پایگاه‌داده
        var otherTenantContext = new TenantContext();
        otherTenantContext.SetContext(Guid.NewGuid(), Guid.NewGuid());
        using var otherDb = new AccountingDbContext(_options, otherTenantContext, null, null);

        var visibleGlCount = await otherDb.GeneralLedgerEntries.CountAsync();
        visibleGlCount.Should().Be(0, "سطرهای دفتر کل مستاجر دیگر باید به‌کلی نامرئی باشند");

        var visibleDocCount = await otherDb.Documents.CountAsync();
        visibleDocCount.Should().Be(0, "اسناد مستاجر دیگر باید به‌کلی نامرئی باشند");

        // مستاجر مبدأ به داده‌های خود دسترسی دارد
        using var ownDb = NewDbContext();
        var ownDoc = await ownDb.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        ownDoc.Should().NotBeNull();
        (await ownDb.GeneralLedgerEntries.CountAsync()).Should().Be(2);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static System.Security.Claims.ClaimsPrincipal CreatePrincipal()
    {
        var identity = new System.Security.Claims.ClaimsIdentity("Bearer");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        return new System.Security.Claims.ClaimsPrincipal(identity);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>کاربر کنترل‌شده برای تست‌های مجوزدهی.</summary>
public sealed class TestUserContext : ICurrentUserContext
{
    public TestUserContext(
        Guid? userId = null,
        bool isAuthenticated = true,
        string[]? permissions = null,
        string[]? roles = null)
    {
        UserId = userId ?? Guid.NewGuid();
        IsAuthenticated = isAuthenticated;
        Permissions = permissions ?? [];
        Roles = roles ?? [];
    }

    public Guid? UserId { get; }
    public string? Email => "security-test@erp.local";
    public bool IsAuthenticated { get; }
    public Guid? ActiveTenantId { get; } = Guid.NewGuid();
    public Guid? ActiveCompanyId { get; } = Guid.NewGuid();
    public Guid? ActiveBranchId => null;
    public IReadOnlyList<string> Roles { get; }
    public IReadOnlyList<string> Permissions { get; }

    public bool HasPermission(string permission) =>
        IsAuthenticated && (Roles.Contains("Admin") || Permissions.Contains(permission));

    public bool IsInRole(string role) => Roles.Contains(role);
}
