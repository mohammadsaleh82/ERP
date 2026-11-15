using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/templates")]
[Authorize]
public class AccountTemplatesController : BaseApiController
{
    private readonly IAccountService _accountService;
    private readonly ITenantContext _tenantContext;

    public AccountTemplatesController(IAccountService accountService, ITenantContext tenantContext)
    {
        _accountService = accountService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var result = await _accountService.GetTemplatesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetTemplateById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accountService.GetTemplateByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("apply")]
    [HasPermission("accounting:accounts:seed")]
    public async Task<IActionResult> ApplyTemplate([FromBody] SeedTemplateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException("هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای اعمال الگوی حسابداری الزامی است.");
        }

        await _accountService.SeedCompanyAccountsFromTemplateAsync(tenantId.Value, companyId.Value, request.TemplateId, cancellationToken);
        return Ok(new { message = "الگوی استاندارد کدینگ حساب‌ها با موفقیت برای شرکت اعمال گردید." });
    }
}
