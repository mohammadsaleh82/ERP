using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IAdjustmentVoucherService
{
    /// <summary>
    /// ایجاد سند اصلاحی متصل به سند مرجع (اصلاح اشتباه ثبت/طبقه‌بندی/مبلغ/تفصیلی).
    /// سند اصلی هرگز بازنویسی نمی‌شود؛ اصلاح سند جدیدی است که با ReferenceDocumentId
    /// به مرجع متصل می‌شود و علت اصلاح در سابقه ممیزی ثبت می‌گردد.
    /// </summary>
    Task<DocumentDto> CreateAdjustmentVoucherAsync(AdjustmentVoucherCommand command, CancellationToken cancellationToken = default);
}

public sealed record AdjustmentVoucherCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid SourceDocumentId,
    DateOnly DocumentDate,
    string Reason,
    IReadOnlyList<VoucherLineCommand> Lines,
    string PerformedBy,
    bool AutoPost = true);

/// <summary>موتور اسناد اصلاحی.</summary>
public class AdjustmentVoucherService : IAdjustmentVoucherService
{
    private readonly AccountingDbContext _db;
    private readonly IDocumentService _documentService;
    private readonly IVoucherPostingEngine _postingEngine;

    public AdjustmentVoucherService(
        AccountingDbContext db,
        IDocumentService documentService,
        IVoucherPostingEngine postingEngine)
    {
        _db = db;
        _documentService = documentService;
        _postingEngine = postingEngine;
    }

    public async Task<DocumentDto> CreateAdjustmentVoucherAsync(AdjustmentVoucherCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new AccountingDomainException("Accounting.AdjustmentReasonRequired",
                "ثبت علت اصلاح برای سند اصلاحی الزامی است.");
        }

        if (command.Lines.Count == 0)
        {
            throw new AccountingDomainException("Accounting.AdjustmentLinesRequired",
                "سند اصلاحی بدون ردیف اصلاح قابل ایجاد نیست.");
        }

        // سند مرجع باید قطعی باشد؛ سند اصلی پس از قطعی تغییرناپذیر است
        var source = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == command.TenantId
                                      && d.CompanyId == command.CompanyId
                                      && d.Id == command.SourceDocumentId, cancellationToken)
            ?? throw new NotFoundException("AccountingDocument", command.SourceDocumentId);

        if (source.Status != DocumentStatus.Posted)
        {
            throw new AccountingDomainException("Accounting.AdjustmentSourceNotPosted",
                $"تنها اسناد قطعی قابل اصلاح هستند؛ وضعیت سند مرجع '{source.DocumentNumber}' '{source.Status}' است.");
        }

        // سال مالیِ تاریخ سند اصلاحی باید باز باشد (اصلاح در سال بسته ممنوع)
        var fiscalYear = await _db.FiscalYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.TenantId == command.TenantId
                                      && y.CompanyId == command.CompanyId
                                      && y.StartDate <= command.DocumentDate
                                      && y.EndDate >= command.DocumentDate, cancellationToken)
            ?? throw new AccountingDomainException("Accounting.DocumentDateOutsideFiscalYear",
                $"تاریخ سند اصلاحی '{command.DocumentDate}' در هیچ سال مالی بازیِ شرکت قرار ندارد.");

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            throw new FiscalYearNotOpenException(fiscalYear.Name, fiscalYear.Status);
        }

        var draft = await _documentService.CreateDraftAsync(new CreateVoucherDraftCommand(
            command.TenantId,
            command.CompanyId,
            fiscalYear.Id,
            command.DocumentDate,
            command.Reason,
            command.Lines,
            DocumentType.Adjustment,
            BranchId: null,
            $"Adj-{source.DocumentNumber}",
            DocumentSourceModules.Accounting,
            source.Id,
            null,
            command.PerformedBy,
            ReferenceDocumentId: source.Id), cancellationToken);

        _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
            command.TenantId, command.CompanyId, draft.Id,
            DocumentAuditActions.Created, null, DocumentStatus.Draft,
            command.PerformedBy,
            $"سند اصلاحی برای سند {source.DocumentNumber} | علت: {command.Reason}"));

        await _db.SaveChangesAsync(cancellationToken);

        if (command.AutoPost)
        {
            await _postingEngine.PostAsync(new PostVoucherCommand(
                command.TenantId, command.CompanyId, draft.Id, null, command.PerformedBy), cancellationToken);
        }

        return draft;
    }
}
