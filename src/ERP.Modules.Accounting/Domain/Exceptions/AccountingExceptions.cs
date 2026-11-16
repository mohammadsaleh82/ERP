using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Domain.Exceptions;

public class AccountingDomainException : AppException
{
    public AccountingDomainException(string code, string message) : base(code, message)
    {
    }

    public AccountingDomainException(string code, string message, Exception innerException) : base(code, message, innerException)
    {
    }

    public AccountingDomainException(string message) : base("Accounting.DomainError", message)
    {
    }
}

public sealed class UnbalancedDocumentException : AccountingDomainException
{
    public decimal TotalDebit { get; }
    public decimal TotalCredit { get; }
    public decimal Difference { get; }

    public UnbalancedDocumentException(decimal totalDebit, decimal totalCredit)
        : base("Accounting.UnbalancedDocument",
            $"سند حسابداری تراز نیست. جمع بدهکار: {totalDebit:N4}، جمع بستانکار: {totalCredit:N4}، اختلاف: {Math.Abs(totalDebit - totalCredit):N4}")
    {
        TotalDebit = totalDebit;
        TotalCredit = totalCredit;
        Difference = Math.Abs(totalDebit - totalCredit);
    }
}

public sealed class ImmutableDocumentException : AccountingDomainException
{
    public Guid DocumentId { get; }
    public string Status { get; }

    public ImmutableDocumentException(Guid documentId, string status)
        : base("Accounting.ImmutableDocument",
            $"سند حسابداری با شناسه '{documentId}' در وضعیت قطعی '{status}' قرار دارد و امکان هرگونه ویرایش، حذف یا تغییر سطرها مسدود است.")
    {
        DocumentId = documentId;
        Status = status;
    }
}

public sealed class FiscalPeriodClosedException : AccountingDomainException
{
    public Guid PeriodId { get; }
    public string Status { get; }

    public FiscalPeriodClosedException(Guid periodId, string status)
        : base("Accounting.FiscalPeriodClosed",
            $"دوره مالی '{periodId}' در وضعیت '{status}' قرار دارد و صدور یا اصلاح سند در آن مجاز نمی‌باشد.")
    {
        PeriodId = periodId;
        Status = status;
    }
}

public sealed class CurrencyMismatchException : AccountingDomainException
{
    public string SourceCurrency { get; }
    public string TargetCurrency { get; }

    public CurrencyMismatchException(string sourceCurrency, string targetCurrency)
        : base("Accounting.CurrencyMismatch",
            $"امکان انجام محاسبات مستقیم مالی بین ارز '{sourceCurrency}' و ارز '{targetCurrency}' بدون نرخ تسعیر ارز وجود ندارد.")
    {
        SourceCurrency = sourceCurrency;
        TargetCurrency = targetCurrency;
    }
}

public sealed class InvalidDocumentTransitionException : AccountingDomainException
{
    public DocumentStatus CurrentStatus { get; }
    public DocumentStatus TargetStatus { get; }
    public DocumentApprovalPolicy ApprovalPolicy { get; }

    public InvalidDocumentTransitionException(DocumentStatus currentStatus, DocumentStatus targetStatus, DocumentApprovalPolicy policy = DocumentApprovalPolicy.DirectPostingAllowed)
        : base("Accounting.InvalidDocumentTransition",
            $"انتقال وضعیت سند از '{currentStatus}' به '{targetStatus}' با سیاست تأیید '{policy}' مجاز نیست. " +
            "چرخه عمر سند باید مطابق ماشین وضعیت استاندارد طی شود.")
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
        ApprovalPolicy = policy;
    }
}

public sealed class DocumentNotInOpenPeriodException : AccountingDomainException
{
    public DateOnly DocumentDate { get; }

    public DocumentNotInOpenPeriodException(DateOnly documentDate)
        : base("Accounting.DocumentDateOutsideOpenPeriod",
            $"تاریخ سند '{documentDate}' خارج از دوره‌های مالی باز است؛ ثبت قطعی سند خارج از دوره مالی مجاز نمی‌باشد.")
    {
        DocumentDate = documentDate;
    }
}

public sealed class FiscalYearNotOpenException : AccountingDomainException
{
    public string YearName { get; }
    public FiscalYearStatus Status { get; }

    public FiscalYearNotOpenException(string yearName, FiscalYearStatus status)
        : base("Accounting.FiscalYearNotOpen",
            $"سال مالی '{yearName}' در وضعیت '{status}' است و صدور سند جدید در آن مجاز نمی‌باشد.")
    {
        YearName = yearName;
        Status = status;
    }
}

public sealed class CrossCompanyAccountException : AccountingDomainException
{
    public Guid AccountId { get; }
    public Guid DocumentCompanyId { get; }

    public CrossCompanyAccountException(Guid accountId, Guid documentCompanyId)
        : base("Accounting.CrossCompanyAccount",
            $"حساب '{accountId}' متعلق به شرکت دیگری است و ثبت سند متعلق به شرکت '{documentCompanyId}' روی آن ممنوع می‌باشد.")
    {
        AccountId = accountId;
        DocumentCompanyId = documentCompanyId;
    }
}

public sealed class IdempotencyKeyConflictException : AccountingDomainException
{
    public string IdempotencyKey { get; }
    public Guid DocumentId { get; }

    public IdempotencyKeyConflictException(string idempotencyKey, Guid documentId)
        : base("Accounting.IdempotencyKeyConflict",
            $"کلید ایدمپوتنسی '{idempotencyKey}' پیش‌تر برای سند دیگری ('{documentId}') استفاده شده است.")
    {
        IdempotencyKey = idempotencyKey;
        DocumentId = documentId;
    }
}

public sealed class UnknownSourceModuleException : AccountingDomainException
{
    public string SourceModule { get; }

    public UnknownSourceModuleException(string sourceModule)
        : base("Accounting.UnknownSourceModule",
            $"منشأ سند '{sourceModule}' شناخته‌شده نیست. منشأهای مجاز: {string.Join(", ", DocumentSourceModules.All)}")
    {
        SourceModule = sourceModule;
    }
}
