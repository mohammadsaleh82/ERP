using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Application.Validators;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IDocumentService
{
    Task<DocumentDto> CreateDraftAsync(CreateVoucherDraftCommand command, CancellationToken cancellationToken = default);
    Task<DocumentDto> UpdateDraftAsync(UpdateVoucherDraftCommand command, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(DeleteVoucherDraftCommand command, CancellationToken cancellationToken = default);
    Task<DocumentDto> SubmitForApprovalAsync(SubmitVoucherForApprovalCommand command, CancellationToken cancellationToken = default);
    Task<DocumentDto> ApproveAsync(ApproveVoucherCommand command, CancellationToken cancellationToken = default);
    Task<DocumentDto> RejectAsync(RejectVoucherCommand command, CancellationToken cancellationToken = default);
    Task<DocumentDetailDto> GetDocumentAsync(Guid tenantId, Guid companyId, Guid documentId, CancellationToken cancellationToken = default);
    Task<PagedDocumentsResult> SearchDocumentsAsync(Guid tenantId, Guid companyId, SearchDocumentsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentAuditLogDto>> GetAuditTrailAsync(Guid tenantId, Guid companyId, Guid documentId, CancellationToken cancellationToken = default);
}

public sealed record PagedDocumentsResult(
    IReadOnlyList<DocumentDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public class DocumentService : IDocumentService
{
    private readonly AccountingDbContext _db;
    private readonly IVoucherNumberingService _numberingService;
    private readonly CreateDocumentRequestValidator _createValidator;
    private readonly UpdateDocumentRequestValidator _updateValidator;

    public DocumentService(
        AccountingDbContext db,
        IVoucherNumberingService numberingService,
        CreateDocumentRequestValidator createValidator,
        UpdateDocumentRequestValidator updateValidator)
    {
        _db = db;
        _numberingService = numberingService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    // ------------------------------------------------------------------
    // Create Draft
    // ------------------------------------------------------------------

    public async Task<DocumentDto> CreateDraftAsync(CreateVoucherDraftCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty || command.CompanyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext", "شناسه مستاجر و شرکت برای ایجاد سند الزامی است.");

        var request = new CreateDocumentRequest(
            command.DocumentDate, command.Description, command.Lines.Select(ToInput).ToList(),
            command.FiscalYearId, command.DocumentType, command.BranchId,
            command.ReferenceNumber, command.SourceModule, command.SourceDocumentId, command.IdempotencyKey);

        await _createValidator.ValidateAndThrowIfInvalidAsync(request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(command.SourceModule) && !DocumentSourceModules.IsKnown(command.SourceModule))
            throw new UnknownSourceModuleException(command.SourceModule);

        // ایدمپوتنسی ایجاد: کلید تکراری سند موجود را بازمی‌گرداند (بدون ایجاد سند دوم)
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await _db.Documents
                .FirstOrDefaultAsync(d => d.IdempotencyKey == command.IdempotencyKey.Trim(), cancellationToken);

            if (existing is not null)
                return MapToDto(existing);
        }

        // تعیین سال مالی: پارامتر ورودی یا سال مالی فعال تنظیمات شرکت
        var fiscalYearId = command.FiscalYearId ?? await ResolveActiveFiscalYearAsync(command.TenantId, command.CompanyId, cancellationToken);

        var fiscalYear = await _db.FiscalYears
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.Id == fiscalYearId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        if (fiscalYear.CompanyId != command.CompanyId)
            throw new NotFoundException("FiscalYear", fiscalYearId);

        if (fiscalYear.Status != FiscalYearStatus.Open)
            throw new FiscalYearNotOpenException(fiscalYear.Name, fiscalYear.Status);

        if (command.DocumentDate < fiscalYear.StartDate || command.DocumentDate > fiscalYear.EndDate)
            throw new AccountingDomainException("Accounting.DocumentDateOutsideFiscalYear",
                $"تاریخ سند '{command.DocumentDate}' خارج از محدوده سال مالی '{fiscalYear.Name}' است.");

        var fiscalPeriod = fiscalYear.Periods.FirstOrDefault(p => p.ContainsDate(command.DocumentDate))
            ?? throw new DocumentNotInOpenPeriodException(command.DocumentDate);

        fiscalPeriod.EnsureCanAcceptTransactions();

        var documentNumber = await _numberingService.NextNumberAsync(
            command.TenantId, command.CompanyId, fiscalYear.Id,
            sequenceType: VoucherSequenceTypes.Voucher,
            periodId: fiscalPeriod.Id,
            cancellationToken);

        var doc = new AccountingDocument(
            command.TenantId,
            command.CompanyId,
            fiscalYear.Id,
            fiscalPeriod.Id,
            documentNumber,
            command.DocumentDate,
            command.Description,
            command.DocumentType,
            command.BranchId,
            command.ReferenceNumber,
            command.SourceModule is null ? null : DocumentSourceModules.Normalize(command.SourceModule),
            command.SourceDocumentId,
            command.IdempotencyKey);

        foreach (var line in command.Lines)
        {
            doc.AddLine(
                line.AccountId,
                line.Side,
                line.Amount,
                line.Description,
                line.CostCenterId,
                line.ProjectId,
                line.DetailAccountId,
                line.ForeignCurrencyCode,
                line.ForeignAmount,
                line.ExchangeRate);
        }

        _db.Documents.Add(doc);

        _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
            command.TenantId, command.CompanyId, doc.Id,
            DocumentAuditActions.Created, null, DocumentStatus.Draft,
            command.PerformedBy,
            $"ایجاد سند پیش‌نویس {doc.DocumentNumber} با {doc.Lines.Count} ردیف"));

        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(doc);
    }

    // ------------------------------------------------------------------
    // Update Draft (اسناد Posted/Reversed و اسناد در جریان تأیید قابل ویرایش نیستند)
    // ------------------------------------------------------------------

    public async Task<DocumentDto> UpdateDraftAsync(UpdateVoucherDraftCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await LoadCompanyDocumentAsync(command.DocumentId, command.CompanyId, cancellationToken);

        if (!doc.IsEditable)
            throw new AccountingDomainException("Accounting.DocumentNotEditable",
                $"سند در وضعیت '{doc.Status}' قابل ویرایش نیست؛ اصلاح اسناد قطعی تنها از مسیر سند معکوس (Reverse) یا سند تعدیل (Adjustment) مجاز است.");

        var request = new UpdateDocumentRequest(
            command.DocumentDate, command.Description, command.Lines.Select(ToInput).ToList(), command.ReferenceNumber);

        await _updateValidator.ValidateAndThrowIfInvalidAsync(request, cancellationToken);

        var previousStatus = doc.Status;

        doc.UpdateDraft(command.DocumentDate, command.Description, command.ReferenceNumber);

        var newLines = new List<AccountingDocumentLine>();
        foreach (var line in command.Lines)
        {
            newLines.Add(new AccountingDocumentLine(
                doc.TenantId, doc.CompanyId, doc.Id,
                line.AccountId,
                newLines.Count + 1,
                line.Side,
                line.Amount,
                line.Description,
                line.CostCenterId,
                line.ProjectId,
                line.DetailAccountId,
                line.ForeignCurrencyCode,
                line.ForeignAmount,
                line.ExchangeRate));
        }

        doc.ReplaceDraftLines(newLines);

        // مهم: ردیف‌های جدید باید صریحاً Add شوند؛ کشف از طریق ناوبری، موجودیت دارای کلید
        // را به‌اشتباه «موجود» فرض کرده و به‌جای INSERT مقدار UPDATE صادر می‌کند.
        _db.DocumentLines.AddRange(newLines);

        _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
            doc.TenantId, doc.CompanyId, doc.Id,
            DocumentAuditActions.Updated, previousStatus, doc.Status,
            command.PerformedBy,
            $"ویرایش سند پیش‌نویس {doc.DocumentNumber}؛ جمع بدهکار {doc.TotalDebit:N4}"));

        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(doc);
    }

    // ------------------------------------------------------------------
    // Delete Draft (حذف فیزیکی تنها برای پیش‌نویس؛ اسناد قطعی هرگز حذف نمی‌شوند)
    // ------------------------------------------------------------------

    public async Task DeleteDraftAsync(DeleteVoucherDraftCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await LoadCompanyDocumentAsync(command.DocumentId, command.CompanyId, cancellationToken);

        if (!doc.IsEditable)
            throw new ImmutableDocumentException(doc.Id, doc.Status.ToString());

        var auditLog = DocumentAuditLog.Create(
            doc.TenantId, doc.CompanyId, doc.Id,
            DocumentAuditActions.Deleted, doc.Status, null,
            command.PerformedBy,
            $"حذف سند پیش‌نویس {doc.DocumentNumber}");

        _db.Documents.Remove(doc);
        _db.DocumentAuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // Submit / Approve / Reject (چرخه تأیید)
    // ------------------------------------------------------------------

    public async Task<DocumentDto> SubmitForApprovalAsync(SubmitVoucherForApprovalCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await LoadCompanyDocumentAsync(command.DocumentId, command.CompanyId, cancellationToken);
        var previousStatus = doc.Status;

        doc.SubmitForApproval(command.PerformedBy);

        _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
            doc.TenantId, doc.CompanyId, doc.Id,
            DocumentAuditActions.Submitted, previousStatus, doc.Status,
            command.PerformedBy));

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(doc);
    }

    public async Task<DocumentDto> ApproveAsync(ApproveVoucherCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await LoadCompanyDocumentAsync(command.DocumentId, command.CompanyId, cancellationToken);
        var previousStatus = doc.Status;

        var policy = await LoadApprovalPolicyAsync(doc.TenantId, doc.CompanyId, cancellationToken);

        doc.Approve(policy, command.PerformedBy);

        _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
            doc.TenantId, doc.CompanyId, doc.Id,
            doc.Status == DocumentStatus.Reviewed ? DocumentAuditActions.Reviewed : DocumentAuditActions.Approved,
            previousStatus, doc.Status,
            command.PerformedBy,
            $"تأیید سند {doc.DocumentNumber} بر اساس سیاست {policy}"));

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(doc);
    }

    public async Task<DocumentDto> RejectAsync(RejectVoucherCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RejectionReason))
            throw new AccountingDomainException("Accounting.EmptyRejectionReason", "دلیل رد سند الزامی است.");

        var doc = await LoadCompanyDocumentAsync(command.DocumentId, command.CompanyId, cancellationToken);
        var previousStatus = doc.Status;

        doc.Reject(command.PerformedBy, command.RejectionReason);

        _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
            doc.TenantId, doc.CompanyId, doc.Id,
            DocumentAuditActions.Rejected, previousStatus, doc.Status,
            command.PerformedBy,
            $"رد سند {doc.DocumentNumber}: {command.RejectionReason.Trim()}"));

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(doc);
    }

    // ------------------------------------------------------------------
    // Queries
    // ------------------------------------------------------------------

    public async Task<DocumentDetailDto> GetDocumentAsync(Guid tenantId, Guid companyId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("AccountingDocument", documentId);

        if (doc.CompanyId != companyId)
            throw new NotFoundException("AccountingDocument", documentId);

        return await MapToDetailDtoAsync(doc, cancellationToken);
    }

    public async Task<PagedDocumentsResult> SearchDocumentsAsync(Guid tenantId, Guid companyId, SearchDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.Documents.AsNoTracking().AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(d => d.Status == request.Status.Value);

        if (request.FiscalYearId.HasValue)
            query = query.Where(d => d.FiscalYearId == request.FiscalYearId.Value);

        if (request.FromDate.HasValue)
            query = query.Where(d => d.DocumentDate >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(d => d.DocumentDate <= request.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(request.SourceModule))
            query = query.Where(d => d.SourceModule == request.SourceModule);

        if (request.SourceDocumentId.HasValue)
            query = query.Where(d => d.SourceDocumentId == request.SourceDocumentId.Value);

        if (request.DocumentNumber.HasValue)
            query = query.Where(d => d.DocumentNumber == request.DocumentNumber.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.DocumentNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedDocumentsResult(
            items.Select(MapToDto).ToList(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    public async Task<IReadOnlyList<DocumentAuditLogDto>> GetAuditTrailAsync(Guid tenantId, Guid companyId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("AccountingDocument", documentId);

        if (doc.CompanyId != companyId)
            throw new NotFoundException("AccountingDocument", documentId);

        var logs = await _db.DocumentAuditLogs.AsNoTracking()
            .Where(l => l.DocumentId == documentId)
            .OrderBy(l => l.PerformedAtUtc)
            .ToListAsync(cancellationToken);

        return logs.Select(l => new DocumentAuditLogDto(
            l.Id, l.DocumentId, l.Action, l.PreviousStatus, l.NewStatus, l.Details, l.PerformedBy, l.PerformedAtUtc)).ToList();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<AccountingDocument> LoadCompanyDocumentAsync(Guid documentId, Guid companyId, CancellationToken cancellationToken)
    {
        var doc = await _db.Documents
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("AccountingDocument", documentId);

        if (doc.CompanyId != companyId)
            throw new NotFoundException("AccountingDocument", documentId);

        return doc;
    }

    private async Task<Guid> ResolveActiveFiscalYearAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        var settings = await _db.AccountingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CompanyId == companyId, cancellationToken);

        if (settings?.ActiveFiscalYearId is { } activeYearId && activeYearId != Guid.Empty)
            return activeYearId;

        throw new AccountingDomainException("Accounting.NoActiveFiscalYear",
            "سال مالی فعال برای شرکت تعیین نشده است؛ شناسه سال مالی را صریحاً وارد کنید یا سال مالی فعال شرکت را تنظیم فرمایید.");
    }

    private async Task<DocumentApprovalPolicy> LoadApprovalPolicyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        var settings = await _db.AccountingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CompanyId == companyId, cancellationToken);

        return settings?.ApprovalPolicy ?? DocumentApprovalPolicy.DirectPostingAllowed;
    }

    private async Task<DocumentDetailDto> MapToDetailDtoAsync(AccountingDocument doc, CancellationToken cancellationToken)
    {
        var accountIds = doc.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var lines = doc.Lines.OrderBy(l => l.LineNumber).Select(l =>
        {
            var account = accounts.GetValueOrDefault(l.AccountId);
            return new DocumentLineDto(
                l.Id, l.AccountId,
                account?.FullCode ?? string.Empty,
                account?.Name ?? string.Empty,
                l.LineNumber, l.DebitCredit, l.DebitAmount, l.CreditAmount, l.Description,
                l.CostCenterId, l.ProjectId, l.DetailAccountId,
                l.ForeignCurrencyCode, l.ForeignCurrencyAmount, l.ExchangeRate);
        }).ToList();

        return new DocumentDetailDto(
            doc.Id, doc.TenantId, doc.CompanyId, doc.FiscalYearId, doc.FiscalPeriodId,
            doc.DocumentNumber, doc.DocumentDate, doc.Description, doc.DocumentType, doc.Status,
            doc.ReferenceNumber, doc.SourceModule, doc.SourceDocumentId, doc.ReversedDocumentId,
            doc.TotalDebit, doc.TotalCredit, doc.IsBalanced, doc.CurrencyCode, doc.RejectionReason,
            doc.CreatedBy, doc.CreatedAtUtc,
            doc.SubmittedBy, doc.SubmittedAtUtc,
            doc.ApprovedBy, doc.ApprovedAtUtc,
            doc.PostedBy, doc.PostedAtUtc,
            lines);
    }

    private static DocumentDto MapToDto(AccountingDocument doc) => new(
        doc.Id, doc.TenantId, doc.CompanyId, doc.FiscalYearId, doc.FiscalPeriodId,
        doc.DocumentNumber, doc.DocumentDate, doc.Description, doc.DocumentType, doc.Status,
        doc.ReferenceNumber, doc.SourceModule, doc.SourceDocumentId, doc.ReversedDocumentId,
        doc.TotalDebit, doc.TotalCredit, doc.CurrencyCode, doc.RejectionReason,
        doc.CreatedBy, doc.CreatedAtUtc,
        doc.SubmittedBy, doc.SubmittedAtUtc,
        doc.ApprovedBy, doc.ApprovedAtUtc,
        doc.PostedBy, doc.PostedAtUtc,
        doc.Lines.Count);

    private static VoucherLineInput ToInput(VoucherLineCommand c) => new(
        c.AccountId, c.Side, c.Amount, c.Description,
        c.CostCenterId, c.ProjectId, c.DetailAccountId,
        c.ForeignCurrencyCode, c.ForeignAmount, c.ExchangeRate);
}
