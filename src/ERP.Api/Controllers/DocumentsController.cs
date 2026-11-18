using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/documents")]
[Authorize]
public class DocumentsController : BaseApiController
{
    private readonly IDocumentService _documentService;
    private readonly IVoucherPostingEngine _postingEngine;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public DocumentsController(
        IDocumentService documentService,
        IVoucherPostingEngine postingEngine,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext)
    {
        _documentService = documentService;
        _postingEngine = postingEngine;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    private (Guid TenantId, Guid CompanyId, string PerformedBy) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException(
                "هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای کار با اسناد حسابداری الزامی است.");
        }

        var performedBy = _currentUserContext.UserId?.ToString() ?? "system";
        return (tenantId.Value, companyId.Value, performedBy);
    }

    /// <summary>ایجاد سند پیش‌نویس.</summary>
    [HttpPost]
    [HasPermission(AccountingPermissions.CreateVouchers)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();

        var command = new CreateVoucherDraftCommand(
            tenantId, companyId,
            request.FiscalYearId,
            request.DocumentDate,
            request.Description,
            request.Lines.Select(l => new VoucherLineCommand(
                l.AccountId, l.Side, l.Amount, l.Description,
                l.CostCenterId, l.ProjectId, l.DetailAccountId,
                l.ForeignCurrencyCode, l.ForeignAmount, l.ExchangeRate)).ToList(),
            request.DocumentType,
            request.BranchId,
            request.ReferenceNumber,
            request.SourceModule,
            request.SourceDocumentId,
            request.IdempotencyKey,
            performedBy);

        var result = await _documentService.CreateDraftAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetDocument), new { id = result.Id }, result);
    }

    /// <summary>ویرایش سند پیش‌نویس (اسناد قطعی قابل ویرایش مستقیم نیستند).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(AccountingPermissions.UpdateDraftVouchers)]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();

        var command = new UpdateVoucherDraftCommand(
            tenantId, companyId, id,
            request.DocumentDate, request.Description,
            request.Lines.Select(l => new VoucherLineCommand(
                l.AccountId, l.Side, l.Amount, l.Description,
                l.CostCenterId, l.ProjectId, l.DetailAccountId,
                l.ForeignCurrencyCode, l.ForeignAmount, l.ExchangeRate)).ToList(),
            request.ReferenceNumber,
            performedBy);

        var result = await _documentService.UpdateDraftAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>حذف سند پیش‌نویس (اسناد قطعی حذف فیزیکی نمی‌شوند).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(AccountingPermissions.DeleteDraftVouchers)]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();
        await _documentService.DeleteDraftAsync(new DeleteVoucherDraftCommand(tenantId, companyId, id, performedBy), cancellationToken);
        return NoContent();
    }

    /// <summary>دریافت سند با جزئیات کامل ردیف‌ها.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(AccountingPermissions.ViewVouchers)]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _documentService.GetDocumentAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    /// <summary>جستجو و صفحه‌بندی اسناد حسابداری.</summary>
    [HttpGet]
    [HasPermission(AccountingPermissions.ViewVouchers)]
    public async Task<IActionResult> SearchDocuments([FromQuery] SearchDocumentsRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _documentService.SearchDocumentsAsync(tenantId, companyId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>سابقه ممیزی کامل چرخه عمر سند.</summary>
    [HttpGet("{id:guid}/audit-trail")]
    [HasPermission(AccountingPermissions.ViewVouchers)]
    public async Task<IActionResult> GetAuditTrail(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _documentService.GetAuditTrailAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    /// <summary>ارسال سند به جریان تأیید (Submit For Approval).</summary>
    [HttpPost("{id:guid}/submit")]
    [HasPermission(AccountingPermissions.SubmitVouchers)]
    public async Task<IActionResult> SubmitForApproval(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();
        var result = await _documentService.SubmitForApprovalAsync(
            new SubmitVoucherForApprovalCommand(tenantId, companyId, id, performedBy), cancellationToken);
        return Ok(result);
    }

    /// <summary>تأیید سند (بر اساس سیاست تأیید شرکت).</summary>
    [HttpPost("{id:guid}/approve")]
    [HasPermission(AccountingPermissions.ApproveVouchers)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();
        var result = await _documentService.ApproveAsync(
            new ApproveVoucherCommand(tenantId, companyId, id, performedBy), cancellationToken);
        return Ok(result);
    }

    /// <summary>رد سند و بازگشت آن به پیش‌نویس با ثبت دلیل.</summary>
    [HttpPost("{id:guid}/reject")]
    [HasPermission(AccountingPermissions.RejectVouchers)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();
        var result = await _documentService.RejectAsync(
            new RejectVoucherCommand(tenantId, companyId, id, request.RejectionReason, performedBy), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// ثبت قطعی اتمیک سند (Posting). با ارائه هدر Idempotency-Key، درخواست تکراری همان نتیجه عملیات نخست را بازمی‌گرداند.
    /// </summary>
    [HttpPost("{id:guid}/post")]
    [HasPermission(AccountingPermissions.PostVouchers)]
    public async Task<IActionResult> Post(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();

        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var headerValue)
            ? headerValue.ToString().Trim()
            : null;

        var result = await _postingEngine.PostAsync(
            new PostVoucherCommand(tenantId, companyId, id, string.IsNullOrEmpty(idempotencyKey) ? null : idempotencyKey, performedBy),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>معکوس‌سازی سند قطعی (Storno) با ایجاد سند معکوس.</summary>
    [HttpPost("{id:guid}/reverse")]
    [HasPermission(AccountingPermissions.ReverseVouchers)]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseDocumentRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();
        var result = await _postingEngine.ReverseAsync(
            new ReverseVoucherCommand(tenantId, companyId, id, request.ReversalDate, request.Description, request.IdempotencyKey, performedBy),
            cancellationToken);
        return Ok(result);
    }
}
