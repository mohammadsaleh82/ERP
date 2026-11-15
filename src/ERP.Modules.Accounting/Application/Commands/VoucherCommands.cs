using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.Commands;

/// <summary>ورودی خط فرمان ایجاد/ویرایش سند.</summary>
public sealed record VoucherLineCommand(
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

public sealed record CreateVoucherDraftCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid? FiscalYearId,
    DateOnly DocumentDate,
    string Description,
    IReadOnlyList<VoucherLineCommand> Lines,
    DocumentType DocumentType,
    Guid? BranchId,
    string? ReferenceNumber,
    string? SourceModule,
    Guid? SourceDocumentId,
    string? IdempotencyKey,
    string PerformedBy);

public sealed record UpdateVoucherDraftCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    DateOnly DocumentDate,
    string Description,
    IReadOnlyList<VoucherLineCommand> Lines,
    string? ReferenceNumber,
    string PerformedBy);

public sealed record DeleteVoucherDraftCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    string PerformedBy);

public sealed record SubmitVoucherForApprovalCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    string PerformedBy);

public sealed record ApproveVoucherCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    string PerformedBy);

public sealed record RejectVoucherCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    string RejectionReason,
    string PerformedBy);

/// <summary>
/// فرمان ثبت قطعی سند (Posting). عملیات اتمیک با Idempotency Key؛
/// درخواست تکراری با کلید یکسان نتیجه عملیات نخست را بازمی‌گرداند.
/// </summary>
public sealed record PostVoucherCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    string? IdempotencyKey,
    string PerformedBy);

public sealed record ReverseVoucherCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    DateOnly ReversalDate,
    string? Description,
    string? IdempotencyKey,
    string PerformedBy);
