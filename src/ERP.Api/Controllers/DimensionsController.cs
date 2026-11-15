using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// مدیریت ابعاد مالی (تفصیلی شناور، مرکز هزینه، پروژه، شعبه، قرارداد، ابعاد سفارشی
/// و آینه مرجع ابعاد متعلق به ماژول‌های دیگر)، سیاست‌های ابعاد حساب‌ها، قواعد
/// ترکیب ممنوع و گزارش‌های تحلیل مالی چندبعدی.
/// </summary>
[ApiController]
[Route("api/v1/accounting/dimensions")]
[Authorize]
public class DimensionsController : BaseApiController
{
    private readonly IFinancialDimensionService _dimensionService;
    private readonly IDimensionAnalysisService _dimensionAnalysis;
    private readonly ITenantContext _tenantContext;

    public DimensionsController(
        IFinancialDimensionService dimensionService,
        IDimensionAnalysisService dimensionAnalysis,
        ITenantContext tenantContext)
    {
        _dimensionService = dimensionService;
        _dimensionAnalysis = dimensionAnalysis;
        _tenantContext = tenantContext;
    }

    private (Guid TenantId, Guid CompanyId) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException(
                "هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای ابعاد مالی الزامی است.");
        }

        return (tenantId.Value, companyId.Value);
    }

    // ------------------------------------------------------------------
    // Dimension registry
    // ------------------------------------------------------------------

    /// <summary>ایجاد بعد مالی داخلی.</summary>
    [HttpPost]
    [HasPermission("accounting:dimensions:write")]
    public async Task<IActionResult> Create([FromBody] CreateDimensionRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.CreateAsync(
            new CreateFinancialDimensionCommand(tenantId, companyId, request.DimensionType, request.Code, request.Name, "system"),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>فهرست صفحه‌بندی‌شده ابعاد مالی با فیلتر.</summary>
    [HttpGet]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> List([FromQuery] FinancialDimensionListQuery query, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var (items, totalCount) = await _dimensionService.ListAsync(tenantId, companyId, query, cancellationToken);
        return Ok(new { Items = items, TotalCount = totalCount, query.PageNumber, query.PageSize });
    }

    /// <summary>دریافت یک بعد مالی.</summary>
    [HttpGet("{dimensionId:guid}")]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> Get(Guid dimensionId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.GetByIdAsync(tenantId, companyId, dimensionId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>ویرایش نام بعد مالی.</summary>
    [HttpPut("{dimensionId:guid}")]
    [HasPermission("accounting:dimensions:write")]
    public async Task<IActionResult> UpdateName(Guid dimensionId, [FromBody] UpdateDimensionNameRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.UpdateNameAsync(tenantId, companyId, dimensionId, request.Name, "system", cancellationToken);
        return Ok(result);
    }

    /// <summary>غیرفعال‌سازی بعد مالی (بعد غیرفعال در اسناد جدید قابل استفاده نیست).</summary>
    [HttpPost("{dimensionId:guid}/deactivate")]
    [HasPermission("accounting:dimensions:write")]
    public async Task<IActionResult> Deactivate(Guid dimensionId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _dimensionService.DeactivateAsync(tenantId, companyId, dimensionId, "system", cancellationToken);
        return NoContent();
    }

    /// <summary>فعال‌سازی مجدد بعد مالی.</summary>
    [HttpPost("{dimensionId:guid}/activate")]
    [HasPermission("accounting:dimensions:write")]
    public async Task<IActionResult> Activate(Guid dimensionId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _dimensionService.ActivateAsync(tenantId, companyId, dimensionId, "system", cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------
    // Account dimension policies
    // ------------------------------------------------------------------

    /// <summary>تعریف/تغییر سیاست بعد مالی یک حساب (اجباری/مجاز/ممنوع).</summary>
    [HttpPost("account-policies")]
    [HasPermission("accounting:dimensions:write")]
    public async Task<IActionResult> SetAccountPolicy([FromBody] SetAccountDimensionPolicyRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.SetAccountPolicyAsync(
            new SetAccountDimensionPolicyCommand(
                tenantId, companyId, request.AccountId, request.DimensionType, request.Mode, "system"),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>سیاست‌های ابعاد مالی یک حساب.</summary>
    [HttpGet("account-policies/{accountId:guid}")]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> GetAccountPolicies(Guid accountId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.GetAccountPoliciesAsync(tenantId, companyId, accountId, cancellationToken);
        return Ok(result);
    }

    // ------------------------------------------------------------------
    // Forbidden combination rules
    // ------------------------------------------------------------------

    /// <summary>تعریف قاعده ترکیب ممنوع ابعاد (سطح نوع یا سطح مقدار).</summary>
    [HttpPost("combination-rules")]
    [HasPermission("accounting:dimensions:write")]
    public async Task<IActionResult> AddCombinationRule([FromBody] AddCombinationRuleRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.AddCombinationRuleAsync(
            new AddDimensionCombinationRuleCommand(
                tenantId, companyId,
                request.FirstDimensionType, request.FirstDimensionId,
                request.SecondDimensionType, request.SecondDimensionId,
                request.Description, "system"),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>قواعد ترکیب ممنوع شرکت.</summary>
    [HttpGet("combination-rules")]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> GetCombinationRules(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionService.GetCombinationRulesAsync(tenantId, companyId, cancellationToken);
        return Ok(result);
    }

    // ------------------------------------------------------------------
    // Financial analysis
    // ------------------------------------------------------------------

    /// <summary>گردش مالی بر اساس نوع بعد مالی (مشتری، تأمین‌کننده، پروژه، مرکز هزینه، شعبه و ...).</summary>
    [HttpGet("analysis/turnover")]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> GetTurnoverByDimension([FromQuery] DimensionTurnoverQuery query, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionAnalysis.GetTurnoverByDimensionAsync(tenantId, companyId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>گردش یک حساب مشخص به تفکیک نوع بعد مالی.</summary>
    [HttpGet("analysis/accounts/{accountId:guid}/turnover")]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> GetAccountTurnover(Guid accountId, [FromQuery] DimensionTurnoverQuery query, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionAnalysis.GetAccountTurnoverByDimensionAsync(tenantId, companyId, accountId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>گزارش ترکیبی چندبعدی (۱ تا ۳ نوع بعد با سقف ردیف).</summary>
    [HttpGet("analysis/combined")]
    [HasPermission("accounting:dimensions:read")]
    public async Task<IActionResult> GetCombinedAnalysis([FromQuery] CombinedDimensionQuery query, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _dimensionAnalysis.GetCombinedAnalysisAsync(tenantId, companyId, query, cancellationToken);
        return Ok(result);
    }
}

// ---------------------------------------------------------------------------
// Request bodies
// ---------------------------------------------------------------------------

public sealed record CreateDimensionRequest(
    FinancialDimensionType DimensionType,
    string Code,
    string Name);

public sealed record UpdateDimensionNameRequest(string Name);

public sealed record SetAccountDimensionPolicyRequest(
    Guid AccountId,
    FinancialDimensionType DimensionType,
    DimensionPolicyMode Mode);

public sealed record AddCombinationRuleRequest(
    FinancialDimensionType FirstDimensionType,
    Guid? FirstDimensionId,
    FinancialDimensionType SecondDimensionType,
    Guid? SecondDimensionId,
    string? Description);
