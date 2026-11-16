using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// گردش‌کار اختتامیه سال مالی — مرحله‌ای، قابل پیگیری و قابل بازیابی.
/// هر اجرا سابقه مراحل (Steps) خود را نگه می‌دارد؛ شکست در هر مرحله وضعیت Failed
/// و علت را ثبت می‌کند و عملیات انجام‌شده از بین نمی‌رود.
/// </summary>
public class FiscalYearClosing : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    /// <summary>حساب مقصد انتقال نتیجه (سود و زیان انباشته).</summary>
    public Guid RetainedEarningsAccountId { get; set; }

    public FiscalClosingStatus Status { get; set; } = FiscalClosingStatus.NotStarted;

    /// <summary>سود (مثبت) یا زیان (منفی) محاسبه‌شده دوره.</summary>
    public decimal? NetIncome { get; set; }

    /// <summary>جمع درآمدهای قطعی سال (علامت‌دار: بستانکار مثبت).</summary>
    public decimal? TotalRevenue { get; set; }

    /// <summary>جمع هزینه‌های قطعی سال (علامت‌دار: بدهکار مثبت).</summary>
    public decimal? TotalExpense { get; set; }

    public Guid? ClosingDocumentId { get; set; }
    public long? ClosingDocumentNumber { get; set; }

    public string? FailedReason { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public string? ClosedBy { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public ICollection<FiscalYearClosingStep> Steps { get; set; } = [];

    private FiscalYearClosing()
    {
    }

    public static FiscalYearClosing Start(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid retainedEarningsAccountId,
        string performedBy,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || fiscalYearId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext", "شناسه مستاجر، شرکت و سال مالی برای اختتامیه الزامی است.");

        if (retainedEarningsAccountId == Guid.Empty)
            throw new AccountingDomainException("Accounting.ClosingRetainedEarningsRequired",
                "حساب مقصد سود و زیان انباشته برای اختتامیه الزامی است.");

        if (string.IsNullOrWhiteSpace(performedBy))
            throw new AccountingDomainException("Accounting.EmptyPerformer", "انجام‌دهنده اختتامیه الزامی است.");

        return new FiscalYearClosing
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            FiscalYearId = fiscalYearId,
            RetainedEarningsAccountId = retainedEarningsAccountId,
            Status = FiscalClosingStatus.Validating,
            StartedAtUtc = nowUtc,
            StartedBy = performedBy.Trim(),
            Version = 1,
            CreatedBy = performedBy.Trim()
        };
    }

    /// <summary>آغاز مرحله جدید؛ سابقه مراحل قبلی حفظ می‌شود (قابلیت بازیابی).</summary>
    public FiscalYearClosingStep BeginStep(int stepNumber, string name)
    {
        var step = FiscalYearClosingStep.Begin(Id, stepNumber, name);
        Steps.Add(step);
        return step;
    }

    public void MarkValidated(IReadOnlyList<string> issues, DateTimeOffset nowUtc)
    {
        if (issues.Count > 0)
        {
            Status = FiscalClosingStatus.Failed;
            FailedReason = string.Join(" | ", issues);
            LastModifiedAtUtc = nowUtc;
            return;
        }

        Status = FiscalClosingStatus.ReadyToClose;
        LastModifiedAtUtc = nowUtc;
    }

    public void MarkClosing(DateTimeOffset nowUtc)
    {
        if (Status != FiscalClosingStatus.ReadyToClose && Status != FiscalClosingStatus.Failed)
            throw new AccountingDomainException("Accounting.ClosingInvalidStateTransition",
                $"اجرای اختتامیه از وضعیت '{Status}' مجاز نیست.");

        Status = FiscalClosingStatus.Closing;
        FailedReason = null;
        LastModifiedAtUtc = nowUtc;
    }

    public void MarkClosed(
        decimal totalRevenue,
        decimal totalExpense,
        decimal netIncome,
        Guid? closingDocumentId,
        long? closingDocumentNumber,
        string performedBy,
        DateTimeOffset nowUtc)
    {
        Status = FiscalClosingStatus.Closed;
        TotalRevenue = totalRevenue;
        TotalExpense = totalExpense;
        NetIncome = netIncome;
        ClosingDocumentId = closingDocumentId;
        ClosingDocumentNumber = closingDocumentNumber;
        ClosedAtUtc = nowUtc;
        ClosedBy = performedBy;
        LastModifiedAtUtc = nowUtc;
        LastModifiedBy = performedBy;
    }

    public void MarkFailed(string reason, DateTimeOffset nowUtc)
    {
        Status = FiscalClosingStatus.Failed;
        FailedReason = reason;
        LastModifiedAtUtc = nowUtc;
    }
}

/// <summary>سابقه یک مرحله از گردش‌کار اختتامیه با خروجی قابل بازبینی.</summary>
public class FiscalYearClosingStep : Entity
{
    public Guid FiscalYearClosingId { get; set; }

    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public FiscalClosingStepStatus Status { get; set; } = FiscalClosingStepStatus.Pending;

    /// <summary>خروجی/گزارش مرحله (فهرست موارد یافت‌شده، مبالغ محاسبه‌شده و ...).</summary>
    public string? Output { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    public FiscalYearClosingStep()
    {
    }

    public static FiscalYearClosingStep Begin(Guid closingId, int stepNumber, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            FiscalYearClosingId = closingId,
            StepNumber = stepNumber,
            Name = name,
            Status = FiscalClosingStepStatus.Pending,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

    public void Complete(string? output)
    {
        Status = FiscalClosingStepStatus.Completed;
        Output = output;
        FinishedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string output)
    {
        Status = FiscalClosingStepStatus.Failed;
        Output = output;
        FinishedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// رکورد بازگشایی سال مالی بسته‌شده — فقط با مجوز ویژه، درخواست‌دهنده و تأییدکننده
/// متمایز، دلیل و بررسی اثر بر افتتاحیه سال بعد. بازگشایی «ساده» وضعیت سال را تغییر
/// نمی‌دهد: دوره‌های بسته‌شده بسته می‌مانند و باید صریحاً بازگشایی شوند.
/// </summary>
public class FiscalYearReopening : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTimeOffset ReopenedAtUtc { get; set; }

    /// <summary>نتیجه بررسی اثر بازگشایی بر افتتاحیه سال بعد.</summary>
    public string NextOpeningImpact { get; set; } = string.Empty;

    /// <summary>خلاصه تغییرات پس از بازگشایی (به‌روزرسانی می‌شود؛ در لحظه بازگشایی توضیح اولیه).</summary>
    public string? ChangesSummary { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    private FiscalYearReopening()
    {
    }

    public static FiscalYearReopening Create(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string reason,
        string requestedBy,
        string approvedBy,
        string nextOpeningImpact,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || fiscalYearId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext", "شناسه مستاجر، شرکت و سال مالی برای بازگشایی الزامی است.");

        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
            throw new AccountingDomainException("Accounting.ReopeningReasonRequired",
                "ثبت دلیل بازگشایی سال مالی الزامی است.");

        requestedBy = requestedBy?.Trim() ?? string.Empty;
        approvedBy = approvedBy?.Trim() ?? string.Empty;

        if (requestedBy.Length == 0 || approvedBy.Length == 0)
            throw new AccountingDomainException("Accounting.ReopeningActorsRequired",
                "ثبت کاربر درخواست‌دهنده و تأییدکننده بازگشایی الزامی است.");

        if (string.Equals(requestedBy, approvedBy, StringComparison.OrdinalIgnoreCase))
            throw new AccountingDomainException("Accounting.ReopeningSelfApproval",
                "تأییدکننده بازگشایی نمی‌تواند همان درخواست‌دهنده باشد.");

        return new FiscalYearReopening
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            FiscalYearId = fiscalYearId,
            Reason = reason,
            RequestedBy = requestedBy,
            ApprovedBy = approvedBy,
            ReopenedAtUtc = nowUtc,
            NextOpeningImpact = nextOpeningImpact,
            Version = 1,
            CreatedBy = approvedBy
        };
    }
}
