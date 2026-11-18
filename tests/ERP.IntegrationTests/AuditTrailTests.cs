using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Services.Audit;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای Audit Trail (PROMPT 09) — ثبت عملیات حساس با کاربر/زمینه/همبستگی/
/// Snapshot پاک‌سازی‌شده و Append-Only بودن از دید API.
/// </summary>
public class AuditTrailTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _year;
    private readonly Account _cash;
    private readonly Account _revenue;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _svc;

    public AuditTrailTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _year = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _revenue = _harness.SeedPostingAccount("4101", "درآمد فروش", nature: AccountNature.Revenue);
        _svc = _harness.NewServices();
    }

    [Fact]
    public void SensitiveKeys_AreRedacted_InSnapshots()
    {
        var snapshot = new Dictionary<string, object?>
        {
            ["userName"] = "admin",
            ["password"] = "super-secret",
            ["apiToken"] = "tok-123",
            ["clientSecret"] = "sec-456",
            ["idempotencyKey"] = "idem-789",
            ["totalDebit"] = 1500m,
            ["description"] = new string('ط', 2000)
        };

        var json = AccountingAuditTrailSanitizerBridge.Serialize(snapshot);

        json.Should().Contain("[REDACTED]");
        json.Should().NotContain("super-secret");
        json.Should().NotContain("tok-123");
        json.Should().NotContain("sec-456");
        json.Should().NotContain("idem-789");
        json.Should().Contain("1500");
        json.Should().Contain("userName");
        // سقف طول Snapshot
        json.Length.Should().BeLessThanOrEqualTo(8000);
    }

    [Fact]
    public async Task VoucherLifecycle_IsFullyAudited_WithCorrelationAndSnapshots()
    {
        var creator = "audit-creator";
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year.Id, new DateOnly(2025, 2, 5),
            "فروش ممیزی",
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 250m, "فروش"),
             new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 250m, "فروش")],
            DocumentType.General, null, null, null, null, null, creator));

        await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, draft.Id, null, creator));

        using var db = _harness.NewDbContext();
        var actions = (await db.AccountingAuditTrails
                .Where(t => t.EntityId == draft.Id)
                .ToListAsync())
            .OrderBy(t => t.TimestampUtc)
            .Select(t => t.Action)
            .ToList();

        actions.Should().Contain("Voucher.Created");
        actions.Should().Contain("Voucher.Posted");

        var created = await db.AccountingAuditTrails
            .FirstAsync(t => t.EntityId == draft.Id && t.Action == "Voucher.Created");

        created.CorrelationId.Should().Be("test-correlation", "شناسه همبستگی از تامین‌کننده زمینه می‌آید");
        created.UserId.Should().Be(creator, "کاربر انجام‌دهنده ثبت می‌شود");
        created.CompanyId.Should().Be(_harness.CompanyId);
        created.EntityType.Should().Be("AccountingDocument");
        created.AfterSnapshot.Should().NotBeNullOrEmpty();
        created.AfterSnapshot.Should().Contain("documentNumber");

        var posted = await db.AccountingAuditTrails
            .FirstAsync(t => t.EntityId == draft.Id && t.Action == "Voucher.Posted");
        posted.BeforeSnapshot.Should().Contain("Draft");
        posted.AfterSnapshot.Should().Contain("Posted");
    }

    [Fact]
    public async Task AccountAndSettingsOperations_AreAudited()
    {
        var accountService = new AccountService(
            _harness.NewDbContext(), _harness.NewAuditTrailService(_harness.NewDbContext()));

        var account = await accountService.CreateAccountAsync(
            _harness.TenantId, _harness.CompanyId,
            new CreateAccountRequest(null, "6201", "حساب ممیزی", "موجودی", AccountNature.Expense, NormalBalance.Debit));
        await accountService.DeactivateAccountAsync(_harness.TenantId, _harness.CompanyId, account.Id);

        var settingsService = new AccountingSettingsService(
            _harness.NewDbContext(), _harness.NewAuditTrailService(_harness.NewDbContext()));
        await settingsService.UpdateSettingsAsync(_harness.TenantId, _harness.CompanyId,
            new UpdateAccountingSettingsRequest("IRR", FiscalCalendarType.Persian, 1, 1, 0,
                RoundingMode.RoundToEven, VoucherNumberingPolicy.ContinuousByFiscalYear,
                AllowNegativeBalancesPolicy.WarningOnly, DocumentApprovalPolicy.DirectPostingAllowed,
                null, null, true, true, RequireSeparateApprover: true, RequireSeparatePoster: false,
                ChangeReason: "فعال‌سازی تفکیک وظایف"));

        using var db = _harness.NewDbContext();
        var actions = await db.AccountingAuditTrails.Select(t => t.Action).ToListAsync();

        actions.Should().Contain("Account.Created");
        actions.Should().Contain("Account.Deactivated");
        actions.Should().Contain("Settings.Updated");
        var settingsAudit = await db.AccountingAuditTrails.FirstAsync(t => t.Action == "Settings.Updated");
        settingsAudit.Reason.Should().Contain("تفکیک وظایف");
        settingsAudit.BeforeSnapshot.Should().NotBeNullOrEmpty();
        settingsAudit.AfterSnapshot.Should().Contain("requireSeparateApprover");
    }

    [Fact]
    public async Task AuditTrail_Query_IsTenantAndCompanyScoped()
    {
        // رکورد در شرکت اول
        var accountService = new AccountService(
            _harness.NewDbContext(), _harness.NewAuditTrailService(_harness.NewDbContext()));
        var account = await accountService.CreateAccountAsync(
            _harness.TenantId, _harness.CompanyId,
            new CreateAccountRequest(null, "6202", "حساب ایزوله", "موجودی", AccountNature.Expense, NormalBalance.Debit));

        // شرکت دیگر روی همان پایگاه‌داده
        var otherTenant = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var otherHarness = VoucherTestHarness.Create(otherTenant, otherCompany, _harness.Connection);
        try
        {
            otherHarness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
            var otherAccountService = new AccountService(
                otherHarness.NewDbContext(), otherHarness.NewAuditTrailService(otherHarness.NewDbContext()));
            await otherAccountService.CreateAccountAsync(
                otherTenant, otherCompany,
                new CreateAccountRequest(null, "6203", "حساب شرکت دیگر", "موجودی", AccountNature.Expense, NormalBalance.Debit));

            // پرس‌وجوی شرکت اول نباید رکورد شرکت دیگر را ببیند
            var query = new AuditTrailQuery();
            var result = await new AccountingAuditTrailService(_harness.NewDbContext(),
                    new SecurityTestClock(), _harness.CurrentUser, new SecurityTestCorrelationProvider())
                .QueryAsync(_harness.TenantId, _harness.CompanyId, query);

            result.Rows.Should().OnlyContain(r => r.CompanyId == _harness.CompanyId);
            result.Rows.Should().Contain(r => r.EntityId == account.Id);
            result.Rows.Should().NotContain(r => r.UserId.Contains("other"));
        }
        finally
        {
            otherHarness.Dispose();
        }
    }

    [Fact]
    public void AuditTrailService_IsAppendOnly()
    {
        // سطح API سرویس: فقط Record و Query — هیچ متود Update/Delete وجود ندارد
        var methods = typeof(IAccountingAuditTrailService).GetMethods()
            .Select(m => m.Name).ToList();
        methods.Should().Contain("RecordAsync");
        methods.Should().Contain("QueryAsync");
        methods.Should().NotContain(m => m.StartsWith("Update") || m.StartsWith("Delete") || m.StartsWith("Remove"));
    }

    public void Dispose() => _harness.Dispose();
}

/// <summary>پل دسترسی به پاک‌ساز برای تست (کلاس داخلی ماژول).</summary>
public static class AccountingAuditTrailSanitizerBridge
{
    public static string Serialize(object? value) => ERP.Modules.Accounting.Domain.Entities.AuditTrailSanitizer.Serialize(value) ?? string.Empty;
}

/// <summary>ساعت ثابت تست‌های امنیتی.</summary>
public sealed class SecurityTestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>تامین شناسه همبستگی ثابت تست‌های امنیتی.</summary>
public sealed class SecurityTestCorrelationProvider : ICorrelationIdProvider
{
    public string CorrelationId { get; set; } = "test-correlation";
    public string GetCorrelationId() => CorrelationId;
}
