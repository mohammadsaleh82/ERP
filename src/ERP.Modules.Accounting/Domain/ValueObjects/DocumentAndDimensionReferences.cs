using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.ValueObjects;

public sealed record AccountingDocumentNumber
{
    public int FiscalYear { get; }
    public long Sequence { get; }
    public string FormattedNumber { get; }
    public string? ManualNumber { get; }

    public AccountingDocumentNumber(int fiscalYear, long sequence, string? manualNumber = null)
    {
        if (fiscalYear < 1300 || fiscalYear > 2200)
            throw new AccountingDomainException("Accounting.InvalidFiscalYearNumber", $"سال مالی '{fiscalYear}' نامعتبر است.");

        if (sequence <= 0)
            throw new AccountingDomainException("Accounting.InvalidDocumentSequence", "شماره ترتیب سند باید عددی مثبت و بزرگتر از صفر باشد.");

        FiscalYear = fiscalYear;
        Sequence = sequence;
        FormattedNumber = $"{fiscalYear}-{sequence:D6}";
        ManualNumber = string.IsNullOrWhiteSpace(manualNumber) ? null : manualNumber.Trim();
    }

    public static AccountingDocumentNumber Create(int fiscalYear, long sequence, string? manualNumber = null) =>
        new(fiscalYear, sequence, manualNumber);

    public override string ToString() =>
        ManualNumber != null ? $"{FormattedNumber} ({ManualNumber})" : FormattedNumber;
}

public sealed record AccountingPeriodReference
{
    public Guid FiscalYearId { get; }
    public Guid FiscalPeriodId { get; }
    public int PeriodNumber { get; }
    public int FiscalYear { get; }

    public AccountingPeriodReference(Guid fiscalYearId, Guid fiscalPeriodId, int periodNumber, int fiscalYear)
    {
        if (fiscalYearId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyFiscalYearId", "شناسه سال مالی نمی‌تواند خالی باشد.");

        if (fiscalPeriodId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyFiscalPeriodId", "شناسه دوره مالی نمی‌تواند خالی باشد.");

        if (periodNumber < 1 || periodNumber > 13)
            throw new AccountingDomainException("Accounting.InvalidPeriodNumber", $"شماره دوره مالی باید بین ۱ تا ۱۳ باشد. مقدار ارسالی: {periodNumber}");

        FiscalYearId = fiscalYearId;
        FiscalPeriodId = fiscalPeriodId;
        PeriodNumber = periodNumber;
        FiscalYear = fiscalYear;
    }

    public override string ToString() => $"سال: {FiscalYear} | دوره: {PeriodNumber}";
}

public sealed record AccountingDimensionReference
{
    public Guid? BranchId { get; }
    public Guid? CostCenterId { get; }
    public Guid? ProjectId { get; }
    public Guid? DetailAccountId { get; }

    public bool HasDimensions =>
        BranchId.HasValue || CostCenterId.HasValue || ProjectId.HasValue || DetailAccountId.HasValue;

    public AccountingDimensionReference(
        Guid? branchId = null,
        Guid? costCenterId = null,
        Guid? projectId = null,
        Guid? detailAccountId = null)
    {
        BranchId = branchId == Guid.Empty ? null : branchId;
        CostCenterId = costCenterId == Guid.Empty ? null : costCenterId;
        ProjectId = projectId == Guid.Empty ? null : projectId;
        DetailAccountId = detailAccountId == Guid.Empty ? null : detailAccountId;
    }

    public static AccountingDimensionReference Empty => new();
}
