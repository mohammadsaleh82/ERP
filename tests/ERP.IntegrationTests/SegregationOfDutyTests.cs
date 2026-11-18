using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// سناریوهای تفکیک وظایف (PROMPT 09) — قواعد قابل تنظیم از تنظیمات شرکت:
/// ثبت‌کننده ≠ تأییدکننده، تأییدکننده ≠ قطعی‌کننده.
/// </summary>
public class SegregationOfDutyTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _year;
    private readonly Account _cash;
    private readonly Account _revenue;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _svc;
    private readonly AccountingSettingsService _settingsService;

    public SegregationOfDutyTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.TwoStepApprovalRequired);
        _year = _harness.SeedOpenFiscalYear(2025);
        _cash = _harness.SeedPostingAccount("1001", "صندوق");
        _revenue = _harness.SeedPostingAccount("4101", "درآمد فروش", nature: AccountNature.Revenue);
        _svc = _harness.NewServices();
        _settingsService = new AccountingSettingsService(
            _harness.NewDbContext(), _harness.NewAuditTrailService(_harness.NewDbContext()));
    }

    private async Task<Guid> CreateDraftAsync(string creator)
    {
        var draft = await _svc.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _year.Id, new DateOnly(2025, 2, 5),
            "فروش تستی تفکیک وظایف",
            [new VoucherLineCommand(_cash.Id, DebitCredit.Debit, 100m, "فروش"),
             new VoucherLineCommand(_revenue.Id, DebitCredit.Credit, 100m, "فروش")],
            DocumentType.General, null, null, null, null, null, creator));
        return draft.Id;
    }

    private UpdateAccountingSettingsRequest SettingsRequest(bool separateApprover, bool separatePoster) =>
        new("IRR", FiscalCalendarType.Persian, 1, 1, 0, RoundingMode.RoundToEven,
            VoucherNumberingPolicy.ContinuousByFiscalYear, AllowNegativeBalancesPolicy.WarningOnly,
            DocumentApprovalPolicy.TwoStepApprovalRequired, null, null,
            AllowUnbalancedDraft: true, AutoNumberingEnabled: true,
            RequireSeparateApprover: separateApprover, RequireSeparatePoster: separatePoster,
            ChangeReason: "سنجش تفکیک وظایف");

    [Fact]
    public async Task CreatorCannotApproveOwnVoucher_WhenPolicyEnabled()
    {
        var creator = "user-creator";
        var approver = "user-approver";
        await _settingsService.UpdateSettingsAsync(_harness.TenantId, _harness.CompanyId, SettingsRequest(separateApprover: true, separatePoster: false));

        var docId = await CreateDraftAsync(creator);
        await _svc.Docs.SubmitForApprovalAsync(new SubmitVoucherForApprovalCommand(
            _harness.TenantId, _harness.CompanyId, docId, creator));

        // ثبت‌کننده می‌کوشد سند خودش را تأیید کند — باید با خطای SoD رد شود
        var act = () => _svc.Docs.ApproveAsync(new ApproveVoucherCommand(_harness.TenantId, _harness.CompanyId, docId, creator));
        await act.Should().ThrowAsync<AccountingDomainException>()
            .Where(e => e.Code == "Accounting.SodCreatorCannotApprove");

        // تأییدکننده متمایز موفق است
        var approved = await _svc.Docs.ApproveAsync(new ApproveVoucherCommand(_harness.TenantId, _harness.CompanyId, docId, approver));
        approved.Status.Should().Be(DocumentStatus.Approved);
    }

    [Fact]
    public async Task CreatorCanApproveOwnVoucher_WhenPolicyDisabled()
    {
        var creator = "user-creator";
        await _settingsService.UpdateSettingsAsync(_harness.TenantId, _harness.CompanyId, SettingsRequest(separateApprover: false, separatePoster: false));

        var docId = await CreateDraftAsync(creator);
        await _svc.Docs.SubmitForApprovalAsync(new SubmitVoucherForApprovalCommand(
            _harness.TenantId, _harness.CompanyId, docId, creator));
        var approved = await _svc.Docs.ApproveAsync(new ApproveVoucherCommand(_harness.TenantId, _harness.CompanyId, docId, creator));
        approved.Status.Should().Be(DocumentStatus.Approved, "سیاست تفکیک فعال نیست");
    }

    [Fact]
    public async Task ApproverCannotPostSameVoucher_WhenPolicyEnabled()
    {
        var creator = "user-creator";
        var approver = "user-approver";
        var poster = "user-poster";
        await _settingsService.UpdateSettingsAsync(_harness.TenantId, _harness.CompanyId, SettingsRequest(separateApprover: true, separatePoster: true));

        var docId = await CreateDraftAsync(creator);
        await _svc.Docs.SubmitForApprovalAsync(new SubmitVoucherForApprovalCommand(
            _harness.TenantId, _harness.CompanyId, docId, creator));
        await _svc.Docs.ApproveAsync(new ApproveVoucherCommand(_harness.TenantId, _harness.CompanyId, docId, approver));

        // تأییدکننده می‌کوشد همان سند را قطعی کند — باید رد شود
        var act = () => _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, docId, null, approver));
        await act.Should().ThrowAsync<AccountingDomainException>()
            .Where(e => e.Code == "Accounting.SodApproverCannotPost");

        // قطعی‌کننده متمایز موفق است
        var posted = await _svc.Engine.PostAsync(new PostVoucherCommand(
            _harness.TenantId, _harness.CompanyId, docId, null, poster));
        posted.DocumentId.Should().Be(docId);
    }

    [Fact]
    public async Task SoDPolicies_AreConfigurableAndPersisted()
    {
        var dto = await _settingsService.UpdateSettingsAsync(
            _harness.TenantId, _harness.CompanyId, SettingsRequest(separateApprover: true, separatePoster: true));

        dto.RequireSeparateApprover.Should().BeTrue();
        dto.RequireSeparatePoster.Should().BeTrue();

        var reloaded = await _settingsService.GetSettingsAsync(_harness.TenantId, _harness.CompanyId);
        reloaded.RequireSeparateApprover.Should().BeTrue();
        reloaded.RequireSeparatePoster.Should().BeTrue();
    }

    public void Dispose() => _harness.Dispose();
}
