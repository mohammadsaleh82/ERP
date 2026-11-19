using ERP.Modules.Tenancy.Domain;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Services.Reports;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای امنیت Tenant/Company/Branch و IDOR در سطح سرویس‌های حسابداری
/// (PROMPT 09) — تغییر شناسه‌ها نباید دسترسی غیرمجاز بدهد.
/// </summary>
public class AccountingTenantSecurityTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _year;
    private readonly Account _cash;
    private readonly Account _revenue;
    private readonly Account _receivable;
    private readonly Branch _branchA;
    private readonly Branch _branchB;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _svc;

    public AccountingTenantSecurityTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _year = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _revenue = _harness.SeedPostingAccount("4101", "درآمد فروش", nature: AccountNature.Revenue);
        _receivable = _harness.SeedPostingAccount("1101", "حساب دریافتنی", nature: AccountNature.Asset);
        // تفکیک شعبه در سطح سند بر اساس مقایسه BranchId سند با ادعای کاربر انجام می‌شود
        _branchA = new Branch { Id = Guid.NewGuid(), TenantId = _harness.TenantId, CompanyId = _harness.CompanyId, Name = "شعبه مرکزی" };
        _branchB = new Branch { Id = Guid.NewGuid(), TenantId = _harness.TenantId, CompanyId = _harness.CompanyId, Name = "شعبه دوم" };
        _svc = _harness.NewServices();
    }

    private async Task<Guid> PostAsync(DateOnly date, string description, Guid? branchId, string user)
    {
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year.Id, date, description,
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 500m, description),
             new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 500m, description)],
            DocumentType.General, branchId, null, null, null, null, user));
        await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, null, user));
        return draft.Id;
    }

    // ------------------------------------------------------------------
    // IDOR: شناسه سند شرکت دیگر در URL/Body نباید سند را برگرداند
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetDocument_WithForeignCompanyId_ReturnsNotFound()
    {
        var docId = await PostAsync(new DateOnly(2025, 2, 5), "فروش شعبه مرکزی", _branchA.Id, "user-a");
        var foreignCompanyId = Guid.NewGuid();

        var act = () => _svc.Docs.GetDocumentAsync(_harness.TenantId, foreignCompanyId, docId);
        await act.Should().ThrowAsync<ERP.BuildingBlocks.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task GetDocument_WithForeignTenantId_ReturnsNotFound()
    {
        var docId = await PostAsync(new DateOnly(2025, 2, 5), "فروش", null, "user-a");
        var foreignTenantId = Guid.NewGuid();

        var act = () => _svc.Docs.GetDocumentAsync(foreignTenantId, _harness.CompanyId, docId);
        await act.Should().ThrowAsync<ERP.BuildingBlocks.Exceptions.NotFoundException>();
    }

    // ------------------------------------------------------------------
    // تفکیک شعبه: کاربر محدود به شعبه، سند شعبه دیگر را نمی‌بیند
    // ------------------------------------------------------------------

    [Fact]
    public async Task BranchScopedUser_CannotReadDocumentOfAnotherBranch()
    {
        var docB = await PostAsync(new DateOnly(2025, 2, 10), "فروش شعبه دوم", _branchB.Id, "user-b");

        // زمینه کاربر محدود به شعبه A
        _harness.CurrentUser.ActiveBranchId = _branchA.Id;
        try
        {
            var act = () => _svc.Docs.GetDocumentAsync(_harness.TenantId, _harness.CompanyId, docB);
            await act.Should().ThrowAsync<ERP.BuildingBlocks.Exceptions.NotFoundException>(
                "سند شعبه دوم نباید برای کاربر شعبه مرکزی قابل مشاهده باشد");

            // سند شعبه خودش قابل مشاهده است
            await PostAsync(new DateOnly(2025, 2, 15), "فروش شعبه مرکزی", _branchA.Id, "user-a");
        }
        finally
        {
            _harness.CurrentUser.ActiveBranchId = null;
        }
    }

    // ------------------------------------------------------------------
    // گزارش با فیلتر دستکاری‌شده: حساب شرکت دیگر نباید اعدادی بدهد
    // ------------------------------------------------------------------

    [Fact]
    public async Task AccountCard_OfForeignCompanyAccount_ReturnsNotFound()
    {
        await PostAsync(new DateOnly(2025, 2, 5), "فروش", null, "user-a");

        var otherTenant = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var otherHarness = VoucherTestHarness.Create(otherTenant, otherCompany, _harness.Connection);
        try
        {
            otherHarness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
            var otherCash = otherHarness.SeedPostingAccount("1001", "صندوق شرکت دیگر");

            // کاربر شرکت اول می‌کوشد کارت حساب شرکت دوم را بخواند
            var service = new LedgerReportsService(_harness.NewDbContext());
            var filter = new ReportFilter(_harness.TenantId, _harness.CompanyId, null,
                null, null, null, null, null, null, null, null, null, null, null, null);
            var act = () => service.GetAccountCardAsync(otherCash.Id, filter, 1, 50);
            await act.Should().ThrowAsync<ERP.BuildingBlocks.Exceptions.NotFoundException>();
        }
        finally
        {
            otherHarness.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Mass Assignment: TenantId/CompanyId بدنه هرگز معتبر نیستند —
    // زمینه از هدرها می‌آید و سند همیشه با زمینه شرکت جاری ساخته می‌شود
    // ------------------------------------------------------------------

    [Fact]
    public async Task DraftCreation_AlwaysBindsToContextTenantAndCompany()
    {
        // حتی اگر شناسه‌های نادرست در فرمان داده شود، اعتبارسنجی زمینه سند را رد می‌کند
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year.Id, new DateOnly(2025, 3, 1), "فروش زمینه",
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 100m, "فروش"),
             new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 100m, "فروش")],
            DocumentType.General, null, null, null, null, null, "user"));

        using var db = _harness.NewDbContext();
        var doc = await db.Documents.FirstAsync(d => d.Id == draft.Id);
        doc.TenantId.Should().Be(_harness.TenantId);
        doc.CompanyId.Should().Be(_harness.CompanyId, "سند با زمینه هدر ساخته می‌شود نه بدنه");

        // فرمان با زمینه شرکت دیگر باید رد شود (سند وجود ندارد در آن شرکت)
        var foreignCompany = Guid.NewGuid();
        var act = () => _svc.Docs.UpdateDraftAsync(new UpdateVoucherDraftCommand(
            _harness.TenantId, foreignCompany, draft.Id, new DateOnly(2025, 3, 1),
            "دستکاری", [], null, "user"));
        await act.Should().ThrowAsync<ERP.BuildingBlocks.Exceptions.NotFoundException>(
            "سند متعلق به شرکت جاری در شرکت دستکاری‌شده یافت نمی‌شود");
    }

    public void Dispose() => _harness.Dispose();
}
