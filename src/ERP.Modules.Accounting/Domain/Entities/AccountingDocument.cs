using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Events;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.ValueObjects;

namespace ERP.Modules.Accounting.Domain.Entities;

public class AccountingDocument : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid? BranchId { get; set; }

    public long DocumentNumber { get; set; }
    public int DailyNumber { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DateTimeOffset RegisterDateUtc { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DocumentType DocumentType { get; set; } = DocumentType.General;

    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? SourceModule { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? ReversedDocumentId { get; set; }

    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public string CurrencyCode { get; set; } = "IRR";
    public string? IdempotencyKey { get; set; }
    public uint Version { get; set; } // Optimistic Concurrency Token

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public List<AccountingDocumentLine> Lines { get; set; } = [];

    public bool IsBalanced => TotalDebit == TotalCredit;
    public decimal ImbalanceAmount => Math.Abs(TotalDebit - TotalCredit);
    public bool IsImmutable => Status is DocumentStatus.Posted or DocumentStatus.Reversed;

    public AccountingDocument()
    {
    }

    public AccountingDocument(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid fiscalPeriodId,
        long documentNumber,
        DateOnly documentDate,
        string description,
        DocumentType documentType = DocumentType.General,
        Guid? branchId = null,
        string? referenceNumber = null,
        string? sourceModule = null,
        Guid? sourceDocumentId = null,
        string? idempotencyKey = null)
    {
        if (tenantId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");
        if (fiscalYearId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyFiscalYearId", "شناسه سال مالی الزامی است.");
        if (fiscalPeriodId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyFiscalPeriodId", "شناسه دوره مالی الزامی است.");
        if (documentNumber <= 0) throw new AccountingDomainException("Accounting.InvalidDocumentNumber", "شماره سند باید مثبت و بزرگتر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(description)) throw new AccountingDomainException("Accounting.EmptyDescription", "شرح سند حسابداری الزامی است.");

        TenantId = tenantId;
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        FiscalPeriodId = fiscalPeriodId;
        BranchId = branchId;
        DocumentNumber = documentNumber;
        DocumentDate = documentDate;
        Description = description.Trim();
        DocumentType = documentType;
        ReferenceNumber = referenceNumber?.Trim();
        SourceModule = sourceModule?.Trim();
        SourceDocumentId = sourceDocumentId;
        IdempotencyKey = idempotencyKey?.Trim();
        RegisterDateUtc = DateTimeOffset.UtcNow;
        Status = DocumentStatus.Draft;

        AddDomainEvent(AccountingDocumentCreatedEvent.Create(Id, TenantId, CompanyId, DocumentNumber, DocumentDate, 0m));
    }

    public AccountingDocumentLine AddLine(
        Guid accountId,
        DebitCredit debitCredit,
        decimal amount,
        string description,
        Guid? costCenterId = null,
        Guid? projectId = null,
        Guid? detailAccountId = null,
        string? foreignCurrencyCode = null,
        decimal? foreignCurrencyAmount = null,
        decimal exchangeRate = 1.0m)
    {
        EnsureMutable();

        if (accountId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyAccountId", "شناسه حساب برای سطر سند الزامی است.");

        if (amount <= 0m)
            throw new AccountingDomainException("Accounting.InvalidLineAmount", "مبلغ سطر سند باید عددی مثبت و بزرگتر از صفر باشد.");

        var lineNumber = Lines.Count + 1;
        var line = new AccountingDocumentLine(
            TenantId,
            CompanyId,
            Id,
            accountId,
            lineNumber,
            debitCredit,
            amount,
            description,
            costCenterId,
            projectId,
            detailAccountId,
            foreignCurrencyCode,
            foreignCurrencyAmount,
            exchangeRate);

        Lines.Add(line);
        RecalculateTotals();
        return line;
    }

    public void RemoveLine(int lineNumber)
    {
        EnsureMutable();

        var line = Lines.FirstOrDefault(l => l.LineNumber == lineNumber);
        if (line is null)
            throw new AccountingDomainException("Accounting.LineNotFound", $"سطر شماره {lineNumber} در سند یافت نشد.");

        Lines.Remove(line);

        // Re-sequence line numbers
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].LineNumber = i + 1;
        }

        RecalculateTotals();
    }

    public void Post()
    {
        EnsureMutable();

        if (Lines.Count < 2)
            throw new AccountingDomainException("Accounting.InsufficientLines", "سند حسابداری جهت ثبت قطعی باید حداقل شامل دو سطر (یک بدهکار و یک بستانکار) باشد.");

        if (!IsBalanced)
            throw new UnbalancedDocumentException(TotalDebit, TotalCredit);

        if (TotalDebit <= 0m)
            throw new AccountingDomainException("Accounting.ZeroTotalDocument", "سند با مبلغ صفر قابل ثبت قطعی نمی‌باشد.");

        Status = DocumentStatus.Posted;

        AddDomainEvent(AccountingDocumentPostedEvent.Create(
            Id,
            TenantId,
            CompanyId,
            DocumentNumber,
            DocumentDate,
            TotalDebit));
    }

    public AccountingDocument CreateReversal(
        long newDocumentNumber,
        DateOnly reversalDate,
        string reversalDescription,
        Guid fiscalPeriodId)
    {
        if (Status != DocumentStatus.Posted)
            throw new AccountingDomainException("Accounting.CannotReverseUnpostedDocument", "تنها اسناد قطعی (Posted) امکان معکوس‌سازی (Storno) دارند.");

        var reversalDoc = new AccountingDocument(
            TenantId,
            CompanyId,
            FiscalYearId,
            fiscalPeriodId,
            newDocumentNumber,
            reversalDate,
            reversalDescription,
            DocumentType.Reversal,
            BranchId,
            referenceNumber: $"Reversal of {DocumentNumber}",
            sourceModule: "Accounting",
            sourceDocumentId: Id)
        {
            ReversedDocumentId = Id
        };

        // Mirror all lines with inverted Debit/Credit
        foreach (var originalLine in Lines.OrderBy(l => l.LineNumber))
        {
            reversalDoc.AddLine(
                originalLine.AccountId,
                originalLine.DebitCredit.Opposite(), // Inverse Debit -> Credit / Credit -> Debit
                originalLine.DebitCredit == DebitCredit.Debit ? originalLine.DebitAmount : originalLine.CreditAmount,
                $"برگشت: {originalLine.Description}",
                originalLine.CostCenterId,
                originalLine.ProjectId,
                originalLine.DetailAccountId,
                originalLine.ForeignCurrencyCode,
                originalLine.ForeignCurrencyAmount,
                originalLine.ExchangeRate);
        }

        Status = DocumentStatus.Reversed;
        ReversedDocumentId = reversalDoc.Id;

        AddDomainEvent(AccountingDocumentReversedEvent.Create(
            Id,
            reversalDoc.Id,
            TenantId,
            CompanyId,
            newDocumentNumber,
            TotalDebit));

        return reversalDoc;
    }

    private void EnsureMutable()
    {
        if (IsImmutable)
            throw new ImmutableDocumentException(Id, Status.ToString());
    }

    private void RecalculateTotals()
    {
        TotalDebit = Lines.Sum(l => l.DebitAmount);
        TotalCredit = Lines.Sum(l => l.CreditAmount);
    }
}

public class AccountingDocumentLine : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid AccountId { get; set; }

    public int LineNumber { get; set; }
    public DebitCredit DebitCredit { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string Description { get; set; } = string.Empty;

    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? DetailAccountId { get; set; }

    public string? ForeignCurrencyCode { get; set; }
    public decimal? ForeignCurrencyAmount { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public AccountingDocument? Document { get; set; }
    public Account? Account { get; set; }

    public AccountingDocumentLine()
    {
    }

    public AccountingDocumentLine(
        Guid tenantId,
        Guid companyId,
        Guid documentId,
        Guid accountId,
        int lineNumber,
        DebitCredit debitCredit,
        decimal amount,
        string description,
        Guid? costCenterId = null,
        Guid? projectId = null,
        Guid? detailAccountId = null,
        string? foreignCurrencyCode = null,
        decimal? foreignCurrencyAmount = null,
        decimal exchangeRate = 1.0m)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        DocumentId = documentId;
        AccountId = accountId;
        LineNumber = lineNumber;
        DebitCredit = debitCredit;
        Description = description?.Trim() ?? string.Empty;
        CostCenterId = costCenterId;
        ProjectId = projectId;
        DetailAccountId = detailAccountId;
        ForeignCurrencyCode = foreignCurrencyCode;
        ForeignCurrencyAmount = foreignCurrencyAmount;
        ExchangeRate = exchangeRate;

        if (debitCredit == DebitCredit.Debit)
        {
            DebitAmount = amount;
            CreditAmount = 0m;
        }
        else
        {
            DebitAmount = 0m;
            CreditAmount = amount;
        }
    }
}
