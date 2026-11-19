using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.DTOs;

// ===========================================================================
// فیلتر مشترک گزارش‌ها
// ===========================================================================

/// <summary>
/// فیلتر مشترک همه گزارش‌های مالی. مرزهای زمانی از سال/دوره مالی یا بازه تاریخ
/// استخراج می‌شود؛ فیلترهای بعدی (شخص/مرکز هزینه/پروژه) روی سطرهای دفتر کل اعمال می‌شوند.
/// </summary>
public sealed record ReportFilter(
    Guid TenantId,
    Guid CompanyId,
    Guid? FiscalYearId = null,
    Guid? FiscalPeriodId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? BranchId = null,
    Guid? AccountId = null,
    string? AccountCodePrefix = null,
    AccountLevel? AccountLevel = null,
    Guid? PartyId = null,
    FinancialDimensionType? PartyDimensionType = null,
    Guid? CostCenterId = null,
    Guid? ProjectId = null,
    string? CurrencyCode = null);

public sealed record ReportRange(
    DateOnly FromDate,
    DateOnly ToDate,
    DateOnly? OpeningBoundary);

// ===========================================================================
// تراز آزمایشی
// ===========================================================================

public sealed record TrialBalanceRow(
    Guid? AccountId,
    string AccountCode,
    string AccountName,
    AccountLevel Level,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record TrialBalanceResultDto(
    Guid CompanyId,
    string? FiscalYearName,
    DateOnly FromDate,
    DateOnly ToDate,
    AccountLevel AggregationLevel,
    bool IsBalanced,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    IReadOnlyList<TrialBalanceRow> Rows);

// ===========================================================================
// دفاتر
// ===========================================================================

public sealed record JournalRowDto(
    long DocumentNumber,
    DateOnly EntryDate,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string Description,
    decimal Debit,
    decimal Credit,
    string CurrencyCode);

public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Rows,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record GeneralLedgerRowDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal DebitTurnover,
    decimal CreditTurnover,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record SubsidiaryLedgerRowDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    long DocumentNumber,
    DateOnly EntryDate,
    string Description,
    decimal Debit,
    decimal Credit,
    string CurrencyCode);

public sealed record AccountCardRowDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    DateOnly EntryDate,
    long DocumentNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string BalanceSide);

public sealed record AccountCardResultDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    string OpeningSide,
    IReadOnlyList<AccountCardRowDto> Rows,
    decimal ClosingBalance,
    string ClosingSide,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record AccountTurnoverDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal DebitTurnover,
    decimal CreditTurnover,
    decimal ClosingDebit,
    decimal ClosingCredit);

// ===========================================================================
// ترازنامه و سود و زیان
// ===========================================================================

public sealed record StatementRowDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Balance,
    ReportAccountCategory Category);

public sealed record StatementSectionDto(
    string Title,
    IReadOnlyList<StatementRowDto> Rows,
    decimal Total);

public sealed record BalanceSheetDto(
    Guid CompanyId,
    DateOnly AsOfDate,
    StatementSectionDto CurrentAssets,
    StatementSectionDto NonCurrentAssets,
    decimal TotalAssets,
    StatementSectionDto CurrentLiabilities,
    StatementSectionDto NonCurrentLiabilities,
    decimal TotalLiabilities,
    StatementSectionDto Equity,
    decimal CurrentPeriodResult,
    decimal TotalEquity,
    bool IsBalanced,
    decimal Difference);

public sealed record ProfitLossDto(
    Guid CompanyId,
    DateOnly FromDate,
    DateOnly ToDate,
    StatementSectionDto Revenues,
    StatementSectionDto CostOfGoodsSold,
    decimal GrossProfit,
    StatementSectionDto OperatingExpenses,
    decimal OperatingProfit,
    StatementSectionDto OtherIncome,
    StatementSectionDto OtherExpenses,
    decimal ProfitBeforeTax,
    StatementSectionDto TaxExpenses,
    decimal NetProfit);

// ===========================================================================
// جریان وجوه نقد (روش مستقیم)
// ===========================================================================

public sealed record CashFlowBucketDto(
    CashFlowCategory Category,
    decimal Inflow,
    decimal Outflow,
    decimal Net);

public sealed record CashFlowDto(
    Guid CompanyId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<CashFlowBucketDto> Buckets,
    decimal OpeningCash,
    decimal ClosingCash,
    bool CashAccountsConfigured,
    string? Note);

// ===========================================================================
// بدهکاران و بستانکاران
// ===========================================================================

public sealed record PartyBalanceRowDto(
    Guid DimensionId,
    string DimensionLabel,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal DebitTurnover,
    decimal CreditTurnover,
    decimal ClosingDebit,
    decimal ClosingCredit,
    decimal SignedBalance);

public sealed record PartyLedgerResultDto(
    string Title,
    bool AccountsConfigured,
    string? Note,
    IReadOnlyList<PartyBalanceRowDto> Rows,
    decimal TotalDebit,
    decimal TotalCredit);

public sealed record AgingBucketDto(
    string Label,
    decimal Amount);

public sealed record AgingResultDto(
    string Title,
    bool HasReliableDueDates,
    string Note,
    DateOnly AsOfDate,
    IReadOnlyList<AgingBucketDto> Buckets,
    decimal TotalAmount);

// ===========================================================================
// تنظیمات طبقه‌بندی
// ===========================================================================

public sealed record ReportingClassificationDto(
    Guid Id,
    string RootCode,
    ReportAccountCategory Category);

public sealed record CashFlowClassificationDto(
    Guid Id,
    Guid AccountId,
    CashFlowCategory Category);
