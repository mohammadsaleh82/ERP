using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// عملیات چرخه سال مالی: سند افتتاحیه، سند اصلاحی، معکوس‌سازی با علت،
/// گردش‌کار مرحله‌ای اختتامیه و بازگشایی رسمی سال بسته‌شده.
/// </summary>
[ApiController]
[Route("api/v1/accounting/fiscal-operations")]
[Authorize]
public class FiscalOperationsController : BaseApiController
{
    private readonly IOpeningVoucherService _openingVoucherService;
    private readonly IAdjustmentVoucherService _adjustmentVoucherService;
    private readonly IVoucherPostingEngine _postingEngine;
    private readonly IFiscalClosingWorkflowService _closingWorkflow;
    private readonly IFiscalYearReopeningService _reopeningService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public FiscalOperationsController(
        IOpeningVoucherService openingVoucherService,
        IAdjustmentVoucherService adjustmentVoucherService,
        IVoucherPostingEngine postingEngine,
        IFiscalClosingWorkflowService closingWorkflow,
        IFiscalYearReopeningService reopeningService,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _openingVoucherService = openingVoucherService;
        _adjustmentVoucherService = adjustmentVoucherService;
        _postingEngine = postingEngine;
        _closingWorkflow = closingWorkflow;
        _reopeningService = reopeningService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    private (Guid TenantId, Guid CompanyId, string User) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException(
                "هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای عملیات چرخه سال مالی الزامی است.");
        }

        return (tenantId.Value, companyId.Value, _currentUser.Email ?? "system");
    }

    // ------------------------------------------------------------------
    // Opening voucher
    // ------------------------------------------------------------------

    /// <summary>ایجاد و قطعی‌سازی سند افتتاحیه سال مالی جدید از مانده‌های تأییدشده سال قبل.</summary>
    [HttpPost("opening-vouchers")]
    [HasPermission("accounting:opening:write")]
    public async Task<IActionResult> CreateOpeningVoucher([FromBody] CreateOpeningVoucherRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, user) = GetRequiredContext();
        var result = await _openingVoucherService.CreateOpeningVoucherAsync(
            new OpeningVoucherCommand(tenantId, companyId, request.FiscalYearId, user, request.Description),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>سند افتتاحیه یک سال مالی.</summary>
    [HttpGet("opening-vouchers/{fiscalYearId:guid}")]
    [HasPermission("accounting:opening:read")]
    public async Task<IActionResult> GetOpeningVoucher(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _openingVoucherService.GetByFiscalYearAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // ------------------------------------------------------------------
    // Adjustment voucher
    // ------------------------------------------------------------------

    /// <summary>ایجاد سند اصلاحی متصل به سند مرجع (سند اصلی بازنویسی نمی‌شود).</summary>
    [HttpPost("adjustment-vouchers")]
    [HasPermission("accounting:adjustments:write")]
    public async Task<IActionResult> CreateAdjustmentVoucher([FromBody] CreateAdjustmentVoucherRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, user) = GetRequiredContext();
        var result = await _adjustmentVoucherService.CreateAdjustmentVoucherAsync(
            new AdjustmentVoucherCommand(
                tenantId, companyId, request.SourceDocumentId, request.DocumentDate,
                request.Reason, request.Lines, user, AutoPost: true),
            cancellationToken);
        return Ok(result);
    }

    // ------------------------------------------------------------------
    // Reversal (با علت اصلاح اجباری)
    // ------------------------------------------------------------------

    /// <summary>معکوس‌سازی سند قطعی با علت اصلاح اجباری و ثبت سابقه کامل.</summary>
    [HttpPost("reversals")]
    [HasPermission(AccountingPermissions.ReverseVouchers)]
    public async Task<IActionResult> ReverseDocumentWithReason([FromBody] ReverseDocumentWithReasonRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, user) = GetRequiredContext();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BadHttpRequestException("ثبت علت اصلاح برای معکوس‌سازی سند الزامی است.");
        }

        var result = await _postingEngine.ReverseAsync(new ReverseVoucherCommand(
            tenantId, companyId, request.DocumentId, request.ReversalDate,
            $"برگشت سند — علت: {request.Reason}", null, user), cancellationToken);

        return Ok(result);
    }

    // ------------------------------------------------------------------
    // Fiscal year closing workflow
    // ------------------------------------------------------------------

    /// <summary>اجرای گردش‌کار مرحله‌ای اختتامیه سال مالی.</summary>
    [HttpPost("closing-workflows")]
    [HasPermission(AccountingPermissions.CloseFiscalYear)]
    public async Task<IActionResult> RunClosing([FromBody] RunFiscalClosingRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, user) = GetRequiredContext();
        var result = await _closingWorkflow.RunClosingAsync(
            new RunFiscalClosingCommand(tenantId, companyId, request.FiscalYearId, request.RetainedEarningsAccountId, user),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>وضعیت و سابقه مراحل اختتامیه یک سال مالی.</summary>
    [HttpGet("closing-workflows/{fiscalYearId:guid}")]
    [HasPermission("accounting:fiscalyear:read")]
    public async Task<IActionResult> GetClosingStatus(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _closingWorkflow.GetStatusAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // ------------------------------------------------------------------
    // Reopening
    // ------------------------------------------------------------------

    /// <summary>بازگشایی رسمی سال مالی بسته‌شده (مجوز ویژه + دلیل + تأییدکننده متمایز).</summary>
    [HttpPost("fiscal-years/{fiscalYearId:guid}/reopen")]
    [HasPermission(AccountingPermissions.ReopenFiscalYear)]
    public async Task<IActionResult> ReopenFiscalYear(Guid fiscalYearId, [FromBody] ReopenFiscalYearRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, user) = GetRequiredContext();
        var result = await _reopeningService.ReopenAsync(
            new ReopenFiscalYearCommand(
                tenantId, companyId, fiscalYearId,
                request.Reason,
                request.RequestedBy ?? user,
                request.ApprovedBy ?? throw new BadHttpRequestException("ثبت تأییدکننده بازگشایی الزامی است.")),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>سابقه بازگشایی‌های یک سال مالی.</summary>
    [HttpGet("fiscal-years/{fiscalYearId:guid}/reopenings")]
    [HasPermission("accounting:fiscalyear:read")]
    public async Task<IActionResult> GetReopeningHistory(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _reopeningService.GetHistoryAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        return Ok(result);
    }
}

// ---------------------------------------------------------------------------
// Request bodies
// ---------------------------------------------------------------------------

public sealed record CreateOpeningVoucherRequest(Guid FiscalYearId, string? Description = null);

public sealed record CreateAdjustmentVoucherRequest(
    Guid SourceDocumentId,
    DateOnly DocumentDate,
    string Reason,
    IReadOnlyList<VoucherLineCommand> Lines);

public sealed record ReverseDocumentWithReasonRequest(Guid DocumentId, DateOnly ReversalDate, string Reason);

public sealed record RunFiscalClosingRequest(Guid FiscalYearId, Guid RetainedEarningsAccountId);

public sealed record ReopenFiscalYearRequest(string Reason, string? RequestedBy = null, string? ApprovedBy = null);
