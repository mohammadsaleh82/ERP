using ERP.BuildingBlocks.Common;
using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.DTOs;

// ---------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------

/// <summary>ورودی یک ردیف سند (بدون Id؛ شناسه‌ها در سمت سرور تولید می‌شوند تا دستکاری DocumentId ممکن نباشد).</summary>
public sealed record VoucherLineInput(
    Guid AccountId,
    DebitCredit Side,
    decimal Amount,
    string Description,
    Guid? CostCenterId = null,
    Guid? ProjectId = null,
    Guid? DetailAccountId = null,
    string? ForeignCurrencyCode = null,
    decimal? ForeignAmount = null,
    decimal ExchangeRate = 1.0m);

public sealed record CreateDocumentRequest(
    DateOnly DocumentDate,
    string Description,
    IReadOnlyList<VoucherLineInput> Lines,
    Guid? FiscalYearId = null,
    DocumentType DocumentType = DocumentType.General,
    Guid? BranchId = null,
    string? ReferenceNumber = null,
    string? SourceModule = null,
    Guid? SourceDocumentId = null,
    string? IdempotencyKey = null);

public sealed record UpdateDocumentRequest(
    DateOnly DocumentDate,
    string Description,
    IReadOnlyList<VoucherLineInput> Lines,
    string? ReferenceNumber = null);

public sealed class SearchDocumentsRequest : PagedRequest
{
    public DocumentStatus? Status { get; init; }
    public Guid? FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? SourceModule { get; init; }
    public Guid? SourceDocumentId { get; init; }
    public long? DocumentNumber { get; init; }
}

public sealed record RejectDocumentRequest(string RejectionReason);

public sealed record ReverseDocumentRequest(
    DateOnly ReversalDate,
    string? Description = null,
    string? IdempotencyKey = null);

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

public sealed record DocumentLineDto(
    Guid Id,
    Guid AccountId,
    string AccountFullCode,
    string AccountName,
    int LineNumber,
    DebitCredit Side,
    decimal DebitAmount,
    decimal CreditAmount,
    string Description,
    Guid? CostCenterId,
    Guid? ProjectId,
    Guid? DetailAccountId,
    string? ForeignCurrencyCode,
    decimal? ForeignCurrencyAmount,
    decimal ExchangeRate);

public sealed record DocumentDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    Guid FiscalPeriodId,
    long DocumentNumber,
    DateOnly DocumentDate,
    string Description,
    DocumentType DocumentType,
    DocumentStatus Status,
    string? ReferenceNumber,
    string? SourceModule,
    Guid? SourceDocumentId,
    Guid? ReversedDocumentId,
    decimal TotalDebit,
    decimal TotalCredit,
    string CurrencyCode,
    string? RejectionReason,
    string? CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAtUtc,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAtUtc,
    string? PostedBy,
    DateTimeOffset? PostedAtUtc,
    int LinesCount);

public sealed record DocumentDetailDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    Guid FiscalPeriodId,
    long DocumentNumber,
    DateOnly DocumentDate,
    string Description,
    DocumentType DocumentType,
    DocumentStatus Status,
    string? ReferenceNumber,
    string? SourceModule,
    Guid? SourceDocumentId,
    Guid? ReversedDocumentId,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    string CurrencyCode,
    string? RejectionReason,
    string? CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAtUtc,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAtUtc,
    string? PostedBy,
    DateTimeOffset? PostedAtUtc,
    IReadOnlyList<DocumentLineDto> Lines);

public sealed record PostDocumentResultDto(
    Guid DocumentId,
    long DocumentNumber,
    decimal TotalDebit,
    decimal TotalCredit,
    DateTimeOffset PostedAtUtc,
    string PostedBy,
    bool WasAlreadyPosted);

public sealed record ReverseDocumentResultDto(
    Guid OriginalDocumentId,
    Guid ReversalDocumentId,
    long ReversalDocumentNumber,
    decimal TotalAmount,
    bool WasAlreadyReversed);

public sealed record DocumentAuditLogDto(
    Guid Id,
    Guid DocumentId,
    string Action,
    DocumentStatus? PreviousStatus,
    DocumentStatus? NewStatus,
    string? Details,
    string PerformedBy,
    DateTimeOffset PerformedAtUtc);
