using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services.Reports;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// گزارش‌های مالی: تراز آزمایشی (۱ تا ۴ سطح)، دفاتر (روزنامه/کل/معین/کارت حساب/
/// گردش)، ترازنامه، سود و زیان، جریان وجوه نقد، بدهکاران/بستانکاران، تنظیمات
/// طبقه‌بندی و خروجی Excel/PDF/CSV. همه محاسبات سمت Backend انجام می‌شود.
/// </summary>
[ApiController]
[Route("api/v1/accounting/reports")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly ITrialBalanceService _trialBalance;
    private readonly ILedgerReportsService _ledgerReports;
    private readonly IFinancialStatementsService _statements;
    private readonly ICashFlowService _cashFlow;
    private readonly IReceivablesPayablesService _receivablesPayables;
    private readonly IReportClassificationService _classification;
    private readonly IReportExportService _exportService;
    private readonly ITenantContext _tenantContext;

    public ReportsController(
        ITrialBalanceService trialBalance,
        ILedgerReportsService ledgerReports,
        IFinancialStatementsService statements,
        ICashFlowService cashFlow,
        IReceivablesPayablesService receivablesPayables,
        IReportClassificationService classification,
        IReportExportService exportService,
        ITenantContext tenantContext)
    {
        _trialBalance = trialBalance;
        _ledgerReports = ledgerReports;
        _statements = statements;
        _cashFlow = cashFlow;
        _receivablesPayables = receivablesPayables;
        _classification = classification;
        _exportService = exportService;
        _tenantContext = tenantContext;
    }

    private (Guid TenantId, Guid CompanyId) GetRequiredContext()
    {
        var tenantId = _tenantContext.TenantId;
        var companyId = _tenantContext.CompanyId;

        if (!tenantId.HasValue || !companyId.HasValue)
        {
            throw new BadHttpRequestException(
                "هدرهای سازمان و شرکت (X-Tenant-Id و X-Company-Id) برای گزارش‌های مالی الزامی است.");
        }

        return (tenantId.Value, companyId.Value);
    }

    private ReportFilter BuildFilter(
        Guid? fiscalYearId, Guid? fiscalPeriodId, DateOnly? from, DateOnly? to,
        Guid? branchId, Guid? accountId, string? accountCodePrefix, AccountLevel? accountLevel,
        Guid? partyId, FinancialDimensionType? partyDimensionType, Guid? costCenterId,
        Guid? projectId, string? currencyCode)
    {
        var (tenantId, companyId) = GetRequiredContext();
        return new ReportFilter(
            tenantId, companyId,
            fiscalYearId, fiscalPeriodId, from, to,
            branchId, accountId, accountCodePrefix, accountLevel,
            partyId, partyDimensionType, costCenterId, projectId, currencyCode);
    }

    // ------------------------------------------------------------------
    // تراز آزمایشی
    // ------------------------------------------------------------------

    /// <summary>تراز آزمایشی با تجمیع یک تا چهارسطحی و سه برش زمانی.</summary>
    [HttpGet("trial-balance")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? accountId, [FromQuery] string? accountCodePrefix,
        [FromQuery] Guid? partyId, [FromQuery] Guid? costCenterId, [FromQuery] Guid? projectId,
        [FromQuery] string? currencyCode,
        [FromQuery] AccountLevel level = AccountLevel.Detail, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, accountId,
            accountCodePrefix, null, partyId, null, costCenterId, projectId, currencyCode);
        var result = await _trialBalance.GetTrialBalanceAsync(filter, level, cancellationToken);
        return Ok(result);
    }

    // ------------------------------------------------------------------
    // دفاتر
    // ------------------------------------------------------------------

    /// <summary>دفتر روزنامه (صفحه‌بندی‌شده).</summary>
    [HttpGet("journal")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetJournal(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? accountId, [FromQuery] string? accountCodePrefix,
        [FromQuery] Guid? partyId, [FromQuery] Guid? costCenterId, [FromQuery] Guid? projectId,
        [FromQuery] string? currencyCode,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, accountId,
            accountCodePrefix, null, partyId, null, costCenterId, projectId, currencyCode);
        return Ok(await _ledgerReports.GetJournalAsync(filter, page, pageSize, cancellationToken));
    }

    /// <summary>دفتر کل — مانده اول دوره، گردش و مانده پایان دوره هر حساب.</summary>
    [HttpGet("general-ledger")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetGeneralLedger(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? accountId, [FromQuery] string? accountCodePrefix,
        [FromQuery] AccountLevel? accountLevel,
        [FromQuery] Guid? partyId, [FromQuery] Guid? costCenterId, [FromQuery] Guid? projectId,
        [FromQuery] string? currencyCode, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, accountId,
            accountCodePrefix, accountLevel, partyId, null, costCenterId, projectId, currencyCode);
        return Ok(await _ledgerReports.GetGeneralLedgerAsync(filter, cancellationToken));
    }

    /// <summary>دفتر معین — ردیف‌های تفصیلی حساب‌های قابل ثبت (صفحه‌بندی‌شده).</summary>
    [HttpGet("subsidiary-ledger")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetSubsidiaryLedger(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? accountId, [FromQuery] string? accountCodePrefix,
        [FromQuery] Guid? partyId, [FromQuery] Guid? costCenterId, [FromQuery] Guid? projectId,
        [FromQuery] string? currencyCode,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, accountId,
            accountCodePrefix, null, partyId, null, costCenterId, projectId, currencyCode);
        return Ok(await _ledgerReports.GetSubsidiaryLedgerAsync(filter, page, pageSize, cancellationToken));
    }

    /// <summary>کارت حساب — گردش ردیفی با مانده تراکمی.</summary>
    [HttpGet("account-card/{accountId:guid}")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetAccountCard(
        Guid accountId,
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? partyId, [FromQuery] Guid? costCenterId,
        [FromQuery] Guid? projectId, [FromQuery] string? currencyCode,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, partyId, null, costCenterId, projectId, currencyCode);
        return Ok(await _ledgerReports.GetAccountCardAsync(accountId, filter, page, pageSize, cancellationToken));
    }

    /// <summary>گردش حساب — برش تجمیعی یک حساب.</summary>
    [HttpGet("account-turnover/{accountId:guid}")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetAccountTurnover(
        Guid accountId,
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? partyId, [FromQuery] Guid? costCenterId,
        [FromQuery] Guid? projectId, [FromQuery] string? currencyCode, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, partyId, null, costCenterId, projectId, currencyCode);
        return Ok(await _ledgerReports.GetAccountTurnoverAsync(accountId, filter, cancellationToken));
    }

    // ------------------------------------------------------------------
    // صورت‌های مالی
    // ------------------------------------------------------------------

    /// <summary>ترازنامه — دارایی/بدهی/حقوق مالکانه با نتیجه دوره جاری.</summary>
    [HttpGet("balance-sheet")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetBalanceSheet(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? to, [FromQuery] Guid? branchId,
        [FromQuery] Guid? costCenterId, [FromQuery] Guid? projectId, [FromQuery] string? currencyCode,
        CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, null, to, branchId, null,
            null, null, null, null, costCenterId, projectId, currencyCode);
        return Ok(await _statements.GetBalanceSheetAsync(filter, cancellationToken));
    }

    /// <summary>صورت سود و زیان بازه.</summary>
    [HttpGet("profit-loss")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetProfitLoss(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? costCenterId, [FromQuery] Guid? projectId,
        [FromQuery] string? currencyCode, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, null, null, costCenterId, projectId, currencyCode);
        return Ok(await _statements.GetProfitLossAsync(filter, cancellationToken));
    }

    /// <summary>جریان وجوه نقد — روش مستقیم با طبقه‌بندی جریان نقد.</summary>
    [HttpGet("cash-flow")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, null, null, null, null, null);
        return Ok(await _cashFlow.GetCashFlowAsync(filter, cancellationToken));
    }

    // ------------------------------------------------------------------
    // بدهکاران و بستانکاران
    // ------------------------------------------------------------------

    /// <summary>بدهکاران — مانده و گردش مشتریان روی حساب‌های دریافتنی.</summary>
    [HttpGet("receivables")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetReceivables(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, null, null, null, null, null);
        return Ok(await _receivablesPayables.GetReceivablesAsync(filter, cancellationToken));
    }

    /// <summary>بستانکاران — مانده و گردش تأمین‌کنندگان روی حساب‌های پرداختنی.</summary>
    [HttpGet("payables")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetPayables(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, null, null, null, null, null);
        return Ok(await _receivablesPayables.GetPayablesAsync(filter, cancellationToken));
    }

    /// <summary>گردش اشخاص (بعد شخص روی کل حساب‌ها).</summary>
    [HttpGet("party-turnover")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetPartyTurnover(
        [FromQuery] Guid? fiscalYearId, [FromQuery] Guid? fiscalPeriodId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fiscalYearId, fiscalPeriodId, from, to, branchId, null,
            null, null, null, null, null, null, null);
        return Ok(await _receivablesPayables.GetPartyTurnoverAsync(filter, cancellationToken));
    }

    /// <summary>گزارش سنی مطالبات — در صورت وجود اطلاعات سررسید معتبر.</summary>
    [HttpGet("aging")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetAging(
        [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(null, null, null, to, null, null,
            null, null, null, null, null, null, null);
        return Ok(await _receivablesPayables.GetAgingAsync(filter, cancellationToken));
    }

    // ------------------------------------------------------------------
    // تنظیمات طبقه‌بندی گزارش‌گیری
    // ------------------------------------------------------------------

    /// <summary>تنظیمات طبقه‌بندی ریشه‌های کدینگ.</summary>
    [HttpGet("classifications")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetClassifications(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        return Ok(await _classification.GetSettingsAsync(tenantId, companyId, cancellationToken));
    }

    /// <summary>ثبت/به‌روزرسانی طبقه‌بندی یک ریشه کدینگ.</summary>
    [HttpPut("classifications")]
    [HasPermission("accounting:reports:classify")]
    public async Task<IActionResult> UpsertClassification([FromBody] UpsertReportingClassificationRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        return Ok(await _classification.UpsertAsync(tenantId, companyId, request.RootCode, request.Category, cancellationToken));
    }

    /// <summary>تنظیمات طبقه‌بندی جریان نقد.</summary>
    [HttpGet("cash-flow-classifications")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> GetCashFlowClassifications(CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        return Ok(await _classification.GetCashFlowSettingsAsync(tenantId, companyId, cancellationToken));
    }

    /// <summary>ثبت/به‌روزرسانی طبقه‌بندی جریان نقد یک حساب.</summary>
    [HttpPut("cash-flow-classifications")]
    [HasPermission("accounting:reports:classify")]
    public async Task<IActionResult> UpsertCashFlowClassification([FromBody] UpsertCashFlowClassificationRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, companyId) = GetRequiredContext();
        return Ok(await _classification.UpsertCashFlowAsync(tenantId, companyId, request.AccountId, request.Category, cancellationToken));
    }

    // ------------------------------------------------------------------
    // خروجی‌ها
    // ------------------------------------------------------------------

    /// <summary>
    /// خروجی گزارش (excel/pdf/csv). منبع اعداد همان سرویس‌های گزارش JSON است —
    /// خروجی با API تطابق کامل دارد.
    /// </summary>
    [HttpPost("export")]
    [HasPermission(AccountingPermissions.ViewFinancialReports)]
    public async Task<IActionResult> ExportReport([FromBody] ReportExportApiRequest request, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(request.FiscalYearId, request.FiscalPeriodId, request.From, request.To,
            request.BranchId, request.AccountId, request.AccountCodePrefix, request.AccountLevel,
            request.PartyId, request.PartyDimensionType, request.CostCenterId, request.ProjectId, request.CurrencyCode);

        var export = await BuildExportRequestAsync(request.ReportName, filter, request.AccountId, cancellationToken);
        var format = Enum.TryParse<ReportExportFormat>(request.Format, ignoreCase: true, out var parsed)
            ? parsed : ReportExportFormat.Excel;

        var file = await _exportService.ExportAsync(export, format, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private async Task<ReportExportRequest> BuildExportRequestAsync(
        string reportName, ReportFilter filter, Guid? accountId, CancellationToken ct) =>
        reportName?.Trim().ToLowerInvariant() switch
        {
            "trialbalance" => ReportExportMapper.FromTrialBalance(await _trialBalance.GetTrialBalanceAsync(filter, AccountLevel.Detail, ct)),
            "journal" => ReportExportMapper.FromJournal((await _ledgerReports.GetJournalAsync(filter, 1, 500, ct)).Rows),
            "generalledger" => ReportExportMapper.FromGeneralLedger(await _ledgerReports.GetGeneralLedgerAsync(filter, ct)),
            "balancesheet" => ReportExportMapper.FromBalanceSheet(await _statements.GetBalanceSheetAsync(filter, ct)),
            "profitloss" => ReportExportMapper.FromProfitLoss(await _statements.GetProfitLossAsync(filter, ct)),
            "cashflow" => ReportExportMapper.FromCashFlow(await _cashFlow.GetCashFlowAsync(filter, ct)),
            "receivables" => ReportExportMapper.FromPartyLedger(await _receivablesPayables.GetReceivablesAsync(filter, ct)),
            "payables" => ReportExportMapper.FromPartyLedger(await _receivablesPayables.GetPayablesAsync(filter, ct)),
            "partyturnover" => ReportExportMapper.FromPartyLedger(await _receivablesPayables.GetPartyTurnoverAsync(filter, ct)),
            "accountcard" when accountId.HasValue => ReportExportMapper.FromAccountCard(
                await _ledgerReports.GetAccountCardAsync(accountId.Value, filter, 1, 500, ct)),
            _ => throw new BadHttpRequestException($"نام گزارش '{reportName}' برای خروجی شناخته‌شده نیست.")
        };
}

public sealed record UpsertReportingClassificationRequest(string RootCode, ReportAccountCategory Category);
public sealed record UpsertCashFlowClassificationRequest(Guid AccountId, CashFlowCategory Category);

public sealed record ReportExportApiRequest(
    string ReportName,
    string Format,
    Guid? FiscalYearId = null,
    Guid? FiscalPeriodId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? BranchId = null,
    Guid? AccountId = null,
    string? AccountCodePrefix = null,
    AccountLevel? AccountLevel = null,
    Guid? PartyId = null,
    FinancialDimensionType? PartyDimensionType = null,
    Guid? CostCenterId = null,
    Guid? ProjectId = null,
    string? CurrencyCode = null);
