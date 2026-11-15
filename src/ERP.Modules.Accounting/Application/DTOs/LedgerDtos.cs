using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.DTOs;

// ---------------------------------------------------------------------------
// Journal (دفتر روزنامه)
// ---------------------------------------------------------------------------

public sealed record JournalLineDto(
    Guid AccountId,
    string AccountFullCode,
    string AccountName,
    int LineNumber,
    string Description,
    decimal Debit,
    decimal Credit);

public sealed record JournalEntryDto(
    Guid DocumentId,
    long DocumentNumber,
    DateOnly DocumentDate,
    string Description,
    string? SourceModule,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<JournalLineDto> Lines);

public sealed record DailyJournalTotalDto(
    DateOnly Date,
    int DocumentsCount,
    decimal TotalDebit,
    decimal TotalCredit);

public sealed record JournalReportDto(
    IReadOnlyList<JournalEntryDto> Entries,
    IReadOnlyList<DailyJournalTotalDto> DailyTotals,
    decimal PeriodTotalDebit,
    decimal PeriodTotalCredit,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed class JournalQueryRequest : BuildingBlocks.Common.PagedRequest
{
    public Guid? FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public long? DocumentNumber { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? BranchId { get; init; }
}

// ---------------------------------------------------------------------------
// Balances (موتور مانده)
// ---------------------------------------------------------------------------

/// <summary>جهت نمایش مانده: بدهکار / بستانکار / بدون مانده.</summary>
public enum LedgerBalanceDirection
{
    None = 0,
    Debtor = 1,    // مانده بدهکار
    Creditor = 2   // مانده بستانکار
}

/// <summary>
/// نتیجه موتور مانده یک حساب.
/// مانده به‌صورت علامت‌دار (Signed) نگهداری می‌شود و «مانده منفی» — یعنی مانده
/// خلاف ماهیت عادی حساب — با جهت مخالف ماهیت و پرچم IsAbnormalBalance نمایش
/// داده می‌شود؛ مقدار عددی نمایش همیشه قدرمطلق مانده است تا علامت هرگز گم نشود.
/// </summary>
public sealed record AccountBalanceResultDto(
    Guid AccountId,
    string AccountFullCode,
    string AccountName,
    NormalBalance NormalBalance,
    decimal OpeningBalance,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingBalance,
    LedgerBalanceDirection OpeningDirection,
    LedgerBalanceDirection ClosingDirection,
    decimal OpeningDisplayBalance,
    decimal ClosingDisplayBalance,
    bool IsAbnormalOpeningBalance,
    bool IsAbnormalClosingBalance);

public sealed record BalanceQueryRequest
{
    public Guid? AccountId { get; init; }
    public Guid? FiscalYearId { get; init; }
    public Guid? PeriodId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? BranchId { get; init; }
}

// ---------------------------------------------------------------------------
// General Ledger (دفتر کل)
// ---------------------------------------------------------------------------

/// <summary>خلاصه گردش و مانده یک حساب کل/معین در بازه مشخص.</summary>
public sealed record AccountLedgerSummaryDto(
    Guid AccountId,
    string AccountFullCode,
    string AccountName,
    NormalBalance NormalBalance,
    decimal OpeningBalance,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingBalance,
    LedgerBalanceDirection ClosingDirection,
    decimal ClosingDisplayBalance,
    bool IsAbnormalClosingBalance);

public sealed record GeneralLedgerReportDto(
    Guid? FiscalYearId,
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalCount,
    int PageNumber,
    int PageSize,
    decimal TotalPeriodDebit,
    decimal TotalPeriodCredit,
    IReadOnlyList<AccountLedgerSummaryDto> Accounts);

public sealed class GeneralLedgerQueryRequest : BuildingBlocks.Common.PagedRequest
{
    public Guid? AccountId { get; init; }
    public Guid? FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? BranchId { get; init; }
    public bool IncludeNonPostingAccounts { get; init; }
}

/// <summary>گردش تفصیلی یک حساب در دفتر کل (سطر به سطر).</summary>
public sealed record LedgerTransactionDto(
    Guid EntryId,
    Guid DocumentId,
    long DocumentNumber,
    int LineNumber,
    DateOnly EntryDate,
    string Description,
    decimal Debit,
    decimal Credit,
    string CurrencyCode,
    string? SourceModule,
    Guid? SourceDocumentId);

public sealed record LedgerTransactionsReportDto(
    Guid AccountId,
    string AccountFullCode,
    string AccountName,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    LedgerBalanceDirection ClosingDirection,
    decimal ClosingDisplayBalance,
    int TotalCount,
    int PageNumber,
    int PageSize,
    IReadOnlyList<LedgerTransactionDto> Transactions);

// ---------------------------------------------------------------------------
// Subsidiary Ledger (دفتر معین)
// ---------------------------------------------------------------------------

public sealed record SubsidiaryLedgerReportDto(
    Guid ParentAccountId,
    string ParentAccountFullCode,
    string ParentAccountName,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<AccountLedgerSummaryDto> SubsidiaryAccounts,
    decimal TotalOpeningBalance,
    decimal TotalPeriodDebit,
    decimal TotalPeriodCredit,
    decimal TotalClosingBalance);

public sealed record SubsidiaryLedgerQueryRequest
{
    public Guid ParentAccountId { get; init; }
    public Guid? FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? BranchId { get; init; }
}

// ---------------------------------------------------------------------------
// Snapshot & Reconciliation (مطابقت‌سنجی)
// ---------------------------------------------------------------------------

public sealed record SnapshotRebuildResultDto(
    int SnapshotsCreated,
    int AccountsCovered,
    decimal TotalDebit,
    decimal TotalCredit);

public sealed record BalanceReconciliationRowDto(
    Guid AccountId,
    Guid FiscalPeriodId,
    decimal SnapshotDebit,
    decimal SnapshotCredit,
    decimal LedgerDebit,
    decimal LedgerCredit,
    decimal DebitDifference,
    decimal CreditDifference);

public sealed record BalanceReconciliationResultDto(
    bool IsConsistent,
    int ComparedRows,
    int MismatchedRows,
    IReadOnlyList<BalanceReconciliationRowDto> Mismatches);

/// <summary>نتیجه تطبیق اسناد قطعی با گردش‌های دفتر کل (Consistency).</summary>
public sealed class LedgerConsistencyResultDto
{
    public bool IsConsistent { get; set; }
    public int PostedDocumentsChecked { get; set; }
    public int DocumentsWithMissingGlEntries { get; set; }
    public int GlEntriesWithoutValidDocument { get; set; }
    public List<Guid> DocumentsWithMissingGlEntriesIds { get; set; } = [];
    public List<Guid> OrphanGlEntryDocumentIds { get; set; } = [];
    public decimal PostedDocumentsTotalDebit { get; set; }
    public decimal PostedDocumentsTotalCredit { get; set; }
    public decimal GlEntriesTotalDebit { get; set; }
    public decimal GlEntriesTotalCredit { get; set; }
}
