using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/fiscal-years")]
[Authorize]
public class FiscalYearsController : BaseApiController
{
    private readonly IFiscalYearService _fiscalYearService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public FiscalYearsController(
        IFiscalYearService fiscalYearService,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext)
    {
        _fiscalYearService = fiscalYearService;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    private (Guid TenantId, Guid CompanyId) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException("هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای دسترسی به دوره‌های مالی الزامی است.");
        }

        return (tenantId.Value, companyId.Value);
    }

    [HttpPost]
    [HasPermission(AccountingPermissions.CreateFiscalYear)]
    public async Task<IActionResult> Create([FromBody] CreateFiscalYearRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _fiscalYearService.CreateFiscalYearAsync(tenantId, companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [HasPermission("accounting:fiscal-years:read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _fiscalYearService.GetFiscalYearsAsync(tenantId, companyId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("accounting:fiscal-years:read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _fiscalYearService.GetFiscalYearByIdAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/open")]
    [HasPermission("accounting:fiscal-years:update")]
    public async Task<IActionResult> Open(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _fiscalYearService.OpenFiscalYearAsync(tenantId, companyId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [HasPermission("accounting:fiscal-years:update")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var closedBy = _currentUserContext.UserId?.ToString() ?? "system";
        await _fiscalYearService.CloseFiscalYearAsync(tenantId, companyId, id, closedBy, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/periods/generate")]
    [HasPermission("accounting:fiscal-years:update")]
    public async Task<IActionResult> GeneratePeriods(Guid id, [FromBody] GeneratePeriodsRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _fiscalYearService.GeneratePeriodsAsync(tenantId, companyId, id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/periods/custom")]
    [HasPermission("accounting:fiscal-years:update")]
    public async Task<IActionResult> CreateCustomPeriod(Guid id, [FromBody] CreateCustomPeriodRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _fiscalYearService.CreateCustomPeriodAsync(tenantId, companyId, id, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/periods")]
    [HasPermission("accounting:fiscal-years:read")]
    public async Task<IActionResult> GetPeriods(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _fiscalYearService.GetPeriodsAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/periods/{periodId:guid}/open")]
    [HasPermission("accounting:fiscal-years:update")]
    public async Task<IActionResult> OpenPeriod(Guid id, Guid periodId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _fiscalYearService.OpenPeriodAsync(tenantId, companyId, id, periodId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/periods/{periodId:guid}/close")]
    [HasPermission(AccountingPermissions.CloseFiscalPeriod)]
    public async Task<IActionResult> ClosePeriod(Guid id, Guid periodId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _fiscalYearService.ClosePeriodAsync(tenantId, companyId, id, periodId, cancellationToken);
        return NoContent();
    }
}
