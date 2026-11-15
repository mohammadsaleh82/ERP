using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Application.Services;

/// <summary>
/// پیاده‌سازی قرارداد بین‌ماژولی حسابداری.
/// ماژول‌های خارجی (Sales, Purchasing, Inventory, Treasury, Payroll) از طریق این قرارداد
/// اسناد سیستمی با منشأ مشخص و قابل ردیابی ایجاد می‌کنند.
/// </summary>
public class AccountingModuleApi : IAccountingModuleApi
{
    private readonly IDocumentService _documentService;
    private readonly IVoucherPostingEngine _postingEngine;

    public AccountingModuleApi(IDocumentService documentService, IVoucherPostingEngine postingEngine)
    {
        _documentService = documentService;
        _postingEngine = postingEngine;
    }

    public async Task<VoucherFromSourceResult> CreateVoucherFromSourceAsync(
        CreateVoucherFromSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!DocumentSourceModules.IsKnown(request.SourceModule))
            throw new UnknownSourceModuleException(request.SourceModule);

        if (request.SourceDocumentId == Guid.Empty)
            throw new AccountingDomainException("Accounting.MissingSourceReference",
                "ایجاد سند سیستمی بدون شناسه رکورد مبدأ (SourceDocumentId) مجاز نیست؛ هر سند سیستمی باید قابل ردیابی باشد.");

        var sourceModule = DocumentSourceModules.Normalize(request.SourceModule);
        var idempotencyKey = request.IdempotencyKey
            ?? $"src:{sourceModule}:{request.SourceDocumentId}";

        var createCommand = new CreateVoucherDraftCommand(
            request.TenantId,
            request.CompanyId,
            request.FiscalYearId,
            request.DocumentDate,
            request.Description,
            request.Lines.Select(l => new VoucherLineCommand(
                l.AccountId, l.Side, l.Amount, l.Description,
                l.CostCenterId, l.ProjectId, l.DetailAccountId,
                l.ForeignCurrencyCode, l.ForeignAmount, l.ExchangeRate,
                l.Dimensions?.Select(d => new DimensionAssignmentCommand(d.DimensionType, d.DimensionId)).ToList())).ToList(),
            DocumentType.General,
            BranchId: null,
            request.ReferenceNumber,
            sourceModule,
            request.SourceDocumentId,
            idempotencyKey,
            request.PerformedBy);

        var draft = await _documentService.CreateDraftAsync(createCommand, cancellationToken);

        if (draft.Status == DocumentStatus.Posted)
        {
            return new VoucherFromSourceResult(
                draft.Id, draft.DocumentNumber, draft.Status,
                draft.TotalDebit, draft.TotalCredit,
                sourceModule, request.SourceDocumentId,
                WasAlreadyExisting: true, WasAlreadyPosted: true);
        }

        if (!request.AutoPost)
        {
            return new VoucherFromSourceResult(
                draft.Id, draft.DocumentNumber, draft.Status,
                draft.TotalDebit, draft.TotalCredit,
                sourceModule, request.SourceDocumentId,
                WasAlreadyExisting: draft.Status != DocumentStatus.Draft || draft.CreatedAtUtc != default,
                WasAlreadyPosted: false);
        }

        var postResult = await _postingEngine.PostAsync(
            new PostVoucherCommand(request.TenantId, request.CompanyId, draft.Id, idempotencyKey, request.PerformedBy),
            cancellationToken);

        return new VoucherFromSourceResult(
            postResult.DocumentId, postResult.DocumentNumber, DocumentStatus.Posted,
            postResult.TotalDebit, postResult.TotalCredit,
            sourceModule, request.SourceDocumentId,
            WasAlreadyExisting: postResult.WasAlreadyPosted,
            WasAlreadyPosted: postResult.WasAlreadyPosted);
    }

    public async Task<IReadOnlyList<VoucherFromSourceSummary>> GetVouchersBySourceAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchDocumentsRequest
        {
            SourceModule = sourceModule,
            SourceDocumentId = sourceDocumentId,
            PageNumber = 1,
            PageSize = 100
        };

        var result = await _documentService.SearchDocumentsAsync(tenantId, companyId, request, cancellationToken);

        return result.Items.Select(d => new VoucherFromSourceSummary(
            d.Id, d.DocumentNumber, d.DocumentDate, d.Status,
            d.TotalDebit, d.TotalCredit, d.ReferenceNumber)).ToList();
    }
}
