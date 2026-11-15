using ERP.BuildingBlocks.Exceptions;

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
