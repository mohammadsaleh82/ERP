using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// گزارش‌های دفتر: دفتر روزنامه، دفتر کل، دفتر معین، موتور مانده،
/// مطابقت‌سنجی اسنپ‌شات‌ها با دفتر کل و تطبیق اسناد قطعی با گردش‌ها.
/// تمام گزارش‌ها فقط اسناد قطعی را ملاک قرار می‌دهند (اسناد Draft اثر ندارند).
/// </summary>
[ApiController]
[Route("api/v1/accounting/ledger")]
[Authorize]
public class LedgerController : BaseApiController
{
    private readonly ILedgerQueryService _ledgerQueries;
    private readonly IBalanceEngine _balanceEngine;
    private readonly IBalanceSnapshotService _balanceSnapshotService;
    private readonly IGlConsistencyService _glConsistencyService;
    private readonly ITenantContext _tenantContext;

    public LedgerController(
        ILedgerQueryService ledgerQueries,
        IBalanceEngine balanceEngine,
        IBalanceSnapshotService balanceSnapshotService,
        IGlConsistencyService glConsistencyService,
        ITenantContext tenantContext)
    {
        _ledgerQueries = ledgerQueries;
        _balanceEngine = balanceEngine;
        _balanceSnapshotService = balanceSnapshotService;
        _glConsistencyService = glConsistencyService;
        _tenantContext = tenantContext;
    }

    private (Guid TenantId, Guid CompanyId, string PerformedBy) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException(
                "هدرهای مشخص‌کننده سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای گزارش‌های دفتر الزامی است.");
        }

        return (tenantId.Value, companyId.Value, "system");
    }

    /// <summary>دفتر روزنامه: اسناد قطعی به ترتیب تاریخ و شماره با جمع روزانه و دوره‌ای.</summary>
    [HttpGet("journal")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> GetJournal([FromQuery] JournalQueryRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _ledgerQueries.GetJournalAsync(tenantId, companyId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>دفتر کل: خلاصه گردش و مانده حساب‌ها (صفحه‌بندی واقعی روی حساب‌ها).</summary>
    [HttpGet("general-ledger")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> GetGeneralLedger([FromQuery] GeneralLedgerQueryRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _ledgerQueries.GetLedgerSummaryAsync(tenantId, companyId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>گردش تفصیلی یک حساب در دفتر کل.</summary>
    [HttpGet("general-ledger/accounts/{accountId:guid}/transactions")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> GetLedgerTransactions(Guid accountId, [FromQuery] GeneralLedgerQueryRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _ledgerQueries.GetLedgerTransactionsAsync(tenantId, companyId, accountId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>دفتر معین: گردش حساب‌های زیرمجموعه یک حساب کل (بدون شمارش دوباره).</summary>
    [HttpGet("subsidiary-ledger")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> GetSubsidiaryLedger([FromQuery] SubsidiaryLedgerQueryRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _ledgerQueries.GetSubsidiaryLedgerAsync(tenantId, companyId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>موتور مانده: Opening/Debit/Credit/Closing بر اساس منبع اصلی دفتر کل.</summary>
    [HttpGet("balances")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> GetBalances([FromQuery] BalanceQueryRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _balanceEngine.GetBalancesAsync(tenantId, companyId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>مانده سریع از اسنپ‌شات‌های تجمیعی (مسیر خواندن بهینه).</summary>
    [HttpGet("balances/snapshot")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> GetSnapshotBalances([FromQuery] Guid fiscalYearId, [FromQuery] Guid? periodId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _balanceSnapshotService.GetSnapshotBalancesAsync(tenantId, companyId, fiscalYearId, periodId, cancellationToken);
        return Ok(result);
    }

    /// <summary>مطابقت‌سنجی اسنپ‌شات‌ها با تجمیع زنده دفتر کل (Reconciliation).</summary>
    [HttpGet("balances/reconcile")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> ReconcileSnapshots([FromQuery] Guid? fiscalYearId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _balanceSnapshotService.ReconcileAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        return Ok(result);
    }

    /// <summary>بازسازی کامل اسنپ‌شات‌ها از منبع اصلی دفتر کل.</summary>
    [HttpPost("balances/snapshots/rebuild")]
    [HasPermission("accounting:ledger:rebuild")]
    public async Task<IActionResult> RebuildSnapshots([FromQuery] Guid? fiscalYearId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, performedBy) = GetRequiredContext();
        var result = await _balanceSnapshotService.RebuildAsync(tenantId, companyId, fiscalYearId, performedBy, cancellationToken);
        return Ok(result);
    }

    /// <summary>تطبیق‌سنجی اسناد قطعی با گردش‌های دفتر کل (هیچ سند بدون گردش و هیچ گردش یتیم).</summary>
    [HttpGet("consistency")]
    [HasPermission(AccountingPermissions.ViewLedger)]
    public async Task<IActionResult> VerifyConsistency([FromQuery] Guid? fiscalYearId, CancellationToken cancellationToken)
    {
        var (tenantId, companyId, _) = GetRequiredContext();
        var result = await _glConsistencyService.VerifyAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        return Ok(result);
    }
}
