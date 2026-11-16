using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Events;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

public class FiscalYear : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public int YearNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get => Name; set => Name = value; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public FiscalCalendarType CalendarType { get; set; } = FiscalCalendarType.Persian;
    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;

    public bool IsClosed => Status == FiscalYearStatus.Closed;
    public bool IsOpen => Status == FiscalYearStatus.Open;

    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public uint Version { get; set; } = 1;

    public ICollection<FiscalPeriod> Periods { get; set; } = [];

    public FiscalYear()
    {
    }

    public FiscalYear(
        Guid tenantId,
        Guid companyId,
        int yearNumber,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        FiscalCalendarType calendarType = FiscalCalendarType.Persian,
        FiscalYearStatus initialStatus = FiscalYearStatus.Open,
        string? title = null)
    {
        if (tenantId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");
        var effectiveName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : (title?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(effectiveName)) throw new AccountingDomainException("Accounting.EmptyFiscalYearName", "عنوان سال مالی الزامی است.");
        if (endDate <= startDate) throw new AccountingDomainException("Accounting.InvalidFiscalYearDates", "تاریخ پایان سال مالی باید پس از تاریخ شروع باشد.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        YearNumber = yearNumber;
        Name = effectiveName;
        StartDate = startDate;
        EndDate = endDate;
        CalendarType = calendarType;
        Status = initialStatus;
    }

    public void EnsureCanModify()
    {
        if (Status == FiscalYearStatus.Closed)
        {
            throw new AccountingDomainException("Accounting.FiscalYearClosed",
                $"سال مالی بسته شده '{Name}' ({YearNumber}) قابل تغییر یا ویرایش نیست.");
        }
    }

    public FiscalPeriod AddPeriod(
        int periodNumber,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        bool isAdjustmentPeriod = false,
        string? title = null)
    {
        EnsureCanModify();

        var effectiveName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : (title?.Trim() ?? string.Empty);

        if (periodNumber < 1 || periodNumber > 24)
            throw new AccountingDomainException("Accounting.InvalidPeriodNumber", "شماره دوره مالی باید بین ۱ تا ۲۴ باشد.");

        if (string.IsNullOrWhiteSpace(effectiveName))
            throw new AccountingDomainException("Accounting.EmptyPeriodName", "عنوان دوره مالی الزامی است.");

        if (endDate < startDate)
            throw new AccountingDomainException("Accounting.InvalidPeriodDates", "تاریخ پایان دوره مالی نمی‌تواند قبل از تاریخ شروع باشد.");

        if (startDate < StartDate || endDate > EndDate)
        {
            throw new AccountingDomainException("Accounting.PeriodOutsideYearBounds",
                $"بازه زمانی دوره مالی '{effectiveName}' ({startDate} تا {endDate}) خارج از محدوده سال مالی ({StartDate} تا {EndDate}) است.");
        }

        if (Periods.Any(p => p.PeriodNumber == periodNumber))
        {
            throw new AccountingDomainException("Accounting.DuplicatePeriodNumber",
                $"دوره مالی با شماره {periodNumber} در سال مالی '{Name}' قبلاً ثبت شده است.");
        }

        // بررسی عدم همپوشانی دوره‌ها (به استثنای دوره تعدیلات که ممکن است هم‌مرز یا پایان سال باشد)
        if (!isAdjustmentPeriod)
        {
            var overlapping = Periods.FirstOrDefault(p =>
                !p.IsAdjustmentPeriod &&
                startDate <= p.EndDate && p.StartDate <= endDate);

            if (overlapping != null)
            {
                throw new AccountingDomainException("Accounting.OverlappingFiscalPeriods",
                    $"دوره مالی '{effectiveName}' ({startDate} تا {endDate}) با دوره '{overlapping.Name}' ({overlapping.StartDate} تا {overlapping.EndDate}) همپوشانی دارد.");
            }
        }

        var period = new FiscalPeriod(
            TenantId,
            CompanyId,
            Id,
            periodNumber,
            effectiveName,
            startDate,
            endDate,
            isAdjustmentPeriod);

        Periods.Add(period);
        return period;
    }

    public FiscalPeriod GetPeriodForDate(DateOnly date)
    {
        var period = Periods.FirstOrDefault(p => p.ContainsDate(date) && p.Status == FiscalPeriodStatus.Open);
        if (period is null)
        {
            throw new AccountingDomainException("Accounting.PeriodNotFoundForDate",
                $"هیچ دوره مالی باز و معتبری برای تاریخ '{date}' در سال مالی '{Name}' یافت نشد.");
        }

        return period;
    }

    public void Open()
    {
        if (Status == FiscalYearStatus.Closed)
        {
            throw new AccountingDomainException("Accounting.FiscalYearClosed", "سال مالی بسته شده به‌صورت عادی قابل بازگشایی نیست.");
        }

        Status = FiscalYearStatus.Open;
    }

    public void StartClosing(string closingBy)
    {
        EnsureCanModify();
        Status = FiscalYearStatus.Closing;
        ClosedBy = closingBy;
    }

    public void Close(string closedBy, DateTimeOffset nowUtc)
    {
        if (Status == FiscalYearStatus.Closed)
            throw new AccountingDomainException("Accounting.AlreadyClosed", "این سال مالی قبلاً بسته شده است.");

        foreach (var period in Periods)
        {
            period.Close();
        }

        Status = FiscalYearStatus.Closed;
        ClosedAtUtc = nowUtc;
        ClosedBy = closedBy;

        AddDomainEvent(FiscalYearClosedEvent.Create(Id, TenantId, CompanyId, YearNumber));
    }

    /// <summary>
    /// بازگشایی سال بسته‌شده — فقط پس از فرایند رسمی بازگشایی (مجوز ویژه + دلیل +
    /// درخواست‌دهنده و تأییدکننده متمایز + بررسی اثر بر افتتاحیه سال بعد).
    /// دوره‌های بسته‌شده «بازگشایی نمی‌شوند»؛ پذیرش سند در هر دوره نیازمند
    /// بازگشایی صریح همان دوره است تا هیچ سندی بدون کنترل قابل ثبت نباشد.
    /// </summary>
    public void Reopen(string reopenedBy, DateTimeOffset nowUtc)
    {
        if (Status != FiscalYearStatus.Closed)
            throw new AccountingDomainException("Accounting.FiscalYearNotClosed",
                $"تنها سال مالی بسته‌شده قابل بازگشایی است؛ وضعیت '{Name}' اکنون '{Status}' است.");

        if (string.IsNullOrWhiteSpace(reopenedBy))
            throw new AccountingDomainException("Accounting.EmptyPerformer", "انجام‌دهنده بازگشایی الزامی است.");

        Status = FiscalYearStatus.Open;
        ClosedAtUtc = null;
        ClosedBy = null;
        LastModifiedBy = reopenedBy;
        LastModifiedAtUtc = nowUtc;
    }
}

public class FiscalPeriod : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    public int PeriodNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get => Name; set => Name = value; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public FiscalPeriodStatus Status { get; set; } = FiscalPeriodStatus.Open;
    public bool IsAdjustmentPeriod { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public FiscalYear? FiscalYear { get; set; }

    public FiscalPeriod()
    {
    }

    public FiscalPeriod(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        int periodNumber,
        string name = "",
        DateOnly startDate = default,
        DateOnly endDate = default,
        bool isAdjustmentPeriod = false,
        FiscalPeriodStatus initialStatus = FiscalPeriodStatus.Open,
        string? title = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PeriodNumber = periodNumber;
        Name = !string.IsNullOrWhiteSpace(name) ? name.Trim() : (title?.Trim() ?? string.Empty);
        StartDate = startDate;
        EndDate = endDate;
        IsAdjustmentPeriod = isAdjustmentPeriod;
        Status = initialStatus;
    }

    public bool ContainsDate(DateOnly date) =>
        date >= StartDate && date <= EndDate;

    public void Open()
    {
        if (Status == FiscalPeriodStatus.Closed)
            throw new AccountingDomainException("Accounting.PeriodAlreadyClosed", $"دوره مالی بسته شده '{Name}' قابل بازگشایی نیست.");

        Status = FiscalPeriodStatus.Open;
    }

    public void Lock()
    {
        if (Status == FiscalPeriodStatus.Closed)
            throw new FiscalPeriodClosedException(Id, Status.ToString());

        Status = FiscalPeriodStatus.Locked;
    }

    public void Unlock()
    {
        if (Status == FiscalPeriodStatus.Closed)
            throw new FiscalPeriodClosedException(Id, Status.ToString());

        Status = FiscalPeriodStatus.Open;
    }

    public void Close()
    {
        Status = FiscalPeriodStatus.Closed;
    }

    public void EnsureCanAcceptTransactions()
    {
        if (Status != FiscalPeriodStatus.Open)
        {
            throw new FiscalPeriodClosedException(Id,
                $"ثبت سند در دوره مالی '{Name}' مجاز نیست زیرا وضعیت دوره '{Status}' می‌باشد.");
        }
    }
}
