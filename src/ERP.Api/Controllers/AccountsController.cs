using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/accounts")]
[Authorize]
public class AccountsController : BaseApiController
{
    private readonly IAccountService _accountService;
    private readonly ITenantContext _tenantContext;

    public AccountsController(IAccountService accountService, ITenantContext tenantContext)
    {
        _accountService = accountService;
        _tenantContext = tenantContext;
    }

    private (Guid TenantId, Guid CompanyId) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException("هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای دسترسی به کدینگ حساب‌ها الزامی است.");
        }

        return (tenantId.Value, companyId.Value);
    }

    [HttpPost]
    [HasPermission("accounting:accounts:create")]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.CreateAccountAsync(tenantId, companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("accounting:accounts:update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.UpdateAccountAsync(tenantId, companyId, id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("accounting:accounts:update")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _accountService.DeactivateAccountAsync(tenantId, companyId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [HasPermission("accounting:accounts:update")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _accountService.ActivateAccountAsync(tenantId, companyId, id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("accounting:accounts:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        await _accountService.DeleteAccountAsync(tenantId, companyId, id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.GetAccountByIdAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-code/{code}")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.GetAccountByCodeAsync(tenantId, companyId, code, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tree")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.GetAccountTreeAsync(tenantId, companyId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/children")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.GetChildrenAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/breadcrumb")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetBreadcrumb(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.GetParentsAsync(tenantId, companyId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> Search([FromQuery] AccountFilterRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.SearchAccountsAsync(tenantId, companyId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("postable")]
    [HasPermission("accounting:accounts:read")]
    public async Task<IActionResult> GetPostable(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        var result = await _accountService.GetPostableAccountsAsync(tenantId, companyId, cancellationToken);
        return Ok(result);
    }
}
