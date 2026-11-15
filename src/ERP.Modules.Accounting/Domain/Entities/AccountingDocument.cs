using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Events;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
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

    // Workflow stamps (چرخه عمر سند: Submit / Review / Approve / Reject / Post)
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? PostedAtUtc { get; set; }
    public string? PostedBy { get; set; }

    public List<AccountingDocumentLine> Lines { get; set; } = [];

    public bool IsBalanced => TotalDebit == TotalCredit;
    public decimal ImbalanceAmount => Math.Abs(TotalDebit - TotalCredit);
    public bool IsImmutable => Status is DocumentStatus.Posted or DocumentStatus.Reversed;
    public bool IsEditable => AccountingDocumentWorkflow.CanEditOrDelete(Status);

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

    /// <summary>
    /// ویرایش مشخصات سرآیند سند پیش‌نویس (شرح و تاریخ).
    /// </summary>
    public void UpdateDraft(DateOnly documentDate, string description, string? referenceNumber = null)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingDomainException("Accounting.EmptyDescription", "شرح سند حسابداری الزامی است.");

        DocumentDate = documentDate;
        Description = description.Trim();
        ReferenceNumber = referenceNumber?.Trim();
    }

    /// <summary>
    /// جایگزینی کامل سطرهای سند پیش‌نویس (Update Draft).
    /// </summary>
    public void ReplaceDraftLines(IReadOnlyList<AccountingDocumentLine> newLines)
    {
        EnsureEditable();

        Lines.Clear();
        foreach (var line in newLines)
        {
            line.LineNumber = Lines.Count + 1;
            line.DocumentId = Id;
            Lines.Add(line);
        }

        RecalculateTotals();
    }

    /// <summary>
    /// ارسال سند به جریان تأیید: Draft → PendingApproval.
    /// </summary>
    public void SubmitForApproval(string submittedBy)
    {
        if (string.IsNullOrWhiteSpace(submittedBy))
            throw new AccountingDomainException("Accounting.EmptyActor", "کاربر انجام‌دهنده عملیات ارسال سند الزامی است.");

        AccountingDocumentWorkflow.EnsureCanSubmit(Status);

        if (Lines.Count == 0)
            throw new AccountingDomainException("Accounting.InsufficientLines", "سند بدون ردیف معتبر قابل ارسال به جریان تأیید نیست.");

        Status = DocumentStatus.PendingApproval;
        SubmittedAtUtc = DateTimeOffset.UtcNow;
        SubmittedBy = submittedBy.Trim();

        AddDomainEvent(AccountingDocumentSubmittedEvent.Create(Id, TenantId, CompanyId, DocumentNumber));
    }

    /// <summary>
    /// تأیید سند؛ در سیاست سه‌مرحله‌ای، تأیید نخست سند را «بررسی‌شده» و تأیید دوم «تأییدشده» می‌کند.
    /// </summary>
    public void Approve(DocumentApprovalPolicy policy, string approvedBy)
    {
        if (string.IsNullOrWhiteSpace(approvedBy))
            throw new AccountingDomainException("Accounting.EmptyActor", "کاربر انجام‌دهنده عملیات تأیید سند الزامی است.");

        AccountingDocumentWorkflow.EnsureCanApprove(Status, policy);

        if (Status == DocumentStatus.PendingApproval && policy == DocumentApprovalPolicy.ThreeStepApprovalRequired)
        {
            Status = DocumentStatus.Reviewed;
            ReviewedAtUtc = DateTimeOffset.UtcNow;
            ReviewedBy = approvedBy.Trim();
        }
        else
        {
            Status = DocumentStatus.Approved;
            ApprovedAtUtc = DateTimeOffset.UtcNow;
            ApprovedBy = approvedBy.Trim();
        }
    }

    /// <summary>
    /// رد سند در جریان تأیید و بازگشت آن به پیش‌نویس با ثبت دلیل رد.
    /// </summary>
    public void Reject(string rejectedBy, string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectedBy))
            throw new AccountingDomainException("Accounting.EmptyActor", "کاربر انجام‌دهنده عملیات رد سند الزامی است.");
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new AccountingDomainException("Accounting.EmptyRejectionReason", "دلیل رد سند الزامی است.");

        AccountingDocumentWorkflow.EnsureCanReject(Status);

        Status = DocumentStatus.Draft;
        RejectedAtUtc = DateTimeOffset.UtcNow;
        RejectedBy = rejectedBy.Trim();
        RejectionReason = rejectionReason.Trim();
    }

    /// <summary>
    /// ثبت قطعی سند با کنترل ماشین وضعیت بر اساس سیاست تأیید شرکت.
    /// </summary>
    public void Post(DocumentApprovalPolicy approvalPolicy)
    {
        AccountingDocumentWorkflow.EnsureCanPost(Status, approvalPolicy);

        if (Lines.Count < 2)
            throw new AccountingDomainException("Accounting.InsufficientLines", "سند حسابداری جهت ثبت قطعی باید حداقل شامل دو سطر (یک بدهکار و یک بستانکار) باشد.");

        if (Lines.Any(l => l.DebitAmount <= 0m && l.CreditAmount <= 0m))
            throw new AccountingDomainException("Accounting.ZeroAmountLine", "ردیف بدون مبلغ معتبر مجاز نیست؛ سند دارای ردیف صفر قابل قطعی‌سازی نیست.");

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

    /// <summary>
    /// ثبت اطلاعات ممیزی لحظه قطعی‌سازی (کاربر واقعی و زمان ثبت) — پس از اعتبارسنجی‌های موتور سند.
    /// </summary>
    public void MarkPosted(string postedBy, DateTimeOffset postedAtUtc)
    {
        if (Status != DocumentStatus.Posted)
            throw new AccountingDomainException("Accounting.NotPostedState", "ثبت اطلاعات قطعی‌سازی تنها برای سند در وضعیت Posted مجاز است.");

        PostedBy = postedBy?.Trim();
        PostedAtUtc = postedAtUtc;
    }

    public void Post()
    {
        Post(DocumentApprovalPolicy.DirectPostingAllowed);
    }


    public AccountingDocument CreateReversal(
        long newDocumentNumber,
        DateOnly reversalDate,
        string reversalDescription,
        Guid fiscalPeriodId)
    {
        AccountingDocumentWorkflow.EnsureCanReverse(Status);

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
            // شناسه باید پیش از ارجاع‌دهی متقابل صریحاً تولید شود (تولید EF فقط در لحظه Add انجام می‌شود)
            Id = Guid.NewGuid(),
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

        // سند معکوس بلافاصله پس از ایجاد، قطعی می‌شود (تراز آینه‌ای تضمین‌شده است)
        reversalDoc.Post(DocumentApprovalPolicy.DirectPostingAllowed);

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

    private void EnsureMutable() => EnsureEditable();

    private void EnsureEditable()
    {
        if (IsEditable)
            return;

        if (IsImmutable)
            throw new ImmutableDocumentException(Id, Status.ToString());

        // اسناد در جریان تأیید (PendingApproval / Reviewed) و تأییدشده نیز قابل ویرایش و حذف نیستند
        throw new AccountingDomainException("Accounting.DocumentNotEditable",
            $"سند در وضعیت '{Status}' قابل ویرایش یا حذف نیست؛ تنها اسناد پیش‌نویس (Draft) قابل اصلاح هستند.");
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

        // ردیف بدون مبلغ معتبر مجاز نیست
        if (amount <= 0m)
            throw new AccountingDomainException("Accounting.InvalidLineAmount", "مبلغ ردیف سند باید عددی مثبت و بزرگتر از صفر باشد.");

        if (exchangeRate <= 0m)
            throw new AccountingDomainException("Accounting.InvalidExchangeRate", "نرخ تسعیر ردیف سند باید مثبت باشد.");

        if (foreignCurrencyAmount.HasValue && foreignCurrencyAmount.Value <= 0m)
            throw new AccountingDomainException("Accounting.InvalidForeignAmount", "مبلغ ارزی ردیف سند باید مثبت باشد.");

        if (foreignCurrencyAmount.HasValue && string.IsNullOrWhiteSpace(foreignCurrencyCode))
            throw new AccountingDomainException("Accounting.MissingForeignCurrency", "ثبت مبلغ ارزی بدون کد ارز مجاز نیست.");

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
