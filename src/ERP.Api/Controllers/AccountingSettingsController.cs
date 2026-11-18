using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/settings")]
[Authorize]
public class AccountingSettingsController : BaseApiController
{
    private readonly IAccountingSettingsService _settingsService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public AccountingSettingsController(
        IAccountingSettingsService settingsService,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext)
    {
        _settingsService = settingsService;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    private (Guid TenantId, Guid CompanyId) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException("هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای دسترسی به تنظیمات حسابداری الزامی است.");
        }

        return (tenantId.Value, companyId.Value);
    }

    [HttpGet]
    [HasPermission("accounting:settings:read")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _settingsService.GetSettingsAsync(tenantId, companyId, cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [HasPermission(AccountingPermissions.ManageAccountingSettings)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateAccountingSettingsRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var updatedBy = _currentUserContext.UserId?.ToString() ?? "system";
        var result = await _settingsService.UpdateSettingsAsync(tenantId, companyId, request, updatedBy, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    [HasPermission("accounting:settings:read")]
    public async Task<IActionResult> GetSettingsHistory(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _settingsService.GetSettingsHistoryAsync(tenantId, companyId, cancellationToken);
        return Ok(result);
    }
}
