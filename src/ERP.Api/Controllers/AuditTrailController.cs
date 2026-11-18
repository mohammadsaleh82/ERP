using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// دفتر ممیزی عمومی — فقط خواندنی. ثبت باقی رکوردها از مسیر عملیات حساس انجام
/// می‌شود و هیچ APIای برای تغییر/حذف رکوردهای ممیزی وجود ندارد.
/// </summary>
[ApiController]
[Route("api/v1/accounting/audit-trail")]
[Authorize]
public class AuditTrailController : BaseApiController
{
    private readonly IAccountingAuditTrailService _auditTrail;
    private readonly ITenantContext _tenantContext;

    public AuditTrailController(IAccountingAuditTrailService auditTrail, ITenantContext tenantContext)
    {
        _auditTrail = auditTrail;
        _tenantContext = tenantContext;
    }

    /// <summary>پرس‌وجوی دفتر ممیزی شرکت جاری با فیلترها.</summary>
    [HttpGet]
    [HasPermission("accounting:audit:read")]
    public async Task<IActionResult> Query(
        [FromQuery] string? action, [FromQuery] string? entityType, [FromQuery] Guid? entityId,
        [FromQuery] Guid? branchId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException(
                "هدرهای سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای دفتر ممیزی الزامی است.");
        }

        var query = new AuditTrailQuery(action, entityType, entityId, branchId, from, to, page, pageSize);
        return Ok(await _auditTrail.QueryAsync(tenantId.Value, companyId.Value, query, cancellationToken));
    }
}
