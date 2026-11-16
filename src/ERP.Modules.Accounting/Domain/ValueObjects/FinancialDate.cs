using System.Globalization;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.ValueObjects;

public readonly struct FinancialDate : IEquatable<FinancialDate>, IComparable<FinancialDate>
{
    private static readonly PersianCalendar Calendar = new();

    public DateOnly Date { get; }

    public int Year => Date.Year;
    public int Month => Date.Month;
    public int Day => Date.Day;

    public int PersianYear => Calendar.GetYear(Date.ToDateTime(TimeOnly.MinValue));
    public int PersianMonth => Calendar.GetMonth(Date.ToDateTime(TimeOnly.MinValue));
    public int PersianDay => Calendar.GetDayOfMonth(Date.ToDateTime(TimeOnly.MinValue));

    public FinancialDate(DateOnly date)
    {
        if (date.Year < 1900 || date.Year > 2200)
            throw new AccountingDomainException("Accounting.InvalidFinancialDate", $"تاریخ مالی '{date}' خارج از بازه زمانی مجاز سیستم است.");

        Date = date;
    }

    public FinancialDate(int year, int month, int day)
        : this(new DateOnly(year, month, day))
    {
    }

    public static FinancialDate FromDateOnly(DateOnly date) => new(date);

    public static FinancialDate FromUtc(DateTimeOffset utcDateTime) =>
        new(DateOnly.FromDateTime(utcDateTime.UtcDateTime));

    public static FinancialDate FromPersianDate(int persianYear, int persianMonth, int persianDay)
    {
        try
        {
            var dt = Calendar.ToDateTime(persianYear, persianMonth, persianDay, 0, 0, 0, 0);
            return new FinancialDate(DateOnly.FromDateTime(dt));
        }
        catch (Exception ex)
        {
            throw new AccountingDomainException("Accounting.InvalidPersianDate",
                $"تاریخ شمسی نامعتبر است: {persianYear}/{persianMonth}/{persianDay}", ex);
        }
    }

    public static FinancialDate TodayUtc() =>
        new(DateOnly.FromDateTime(DateTime.UtcNow));

    public DateTime ToUtcDateTime() =>
        DateTime.SpecifyKind(Date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    public DateTimeOffset ToUtcDateTimeOffset() =>
        new(ToUtcDateTime(), TimeSpan.Zero);

    public bool IsBetween(DateOnly start, DateOnly end) =>
        Date >= start && Date <= end;

    public bool IsBetween(FinancialDate start, FinancialDate end) =>
        Date >= start.Date && Date <= end.Date;

    public string ToPersianString() =>
        $"{PersianYear:D4}/{PersianMonth:D2}/{PersianDay:D2}";

    public string ToIsoString() =>
        Date.ToString("yyyy-MM-dd");

    public override string ToString() => ToPersianString();

    public static bool operator >(FinancialDate left, FinancialDate right) => left.Date > right.Date;
    public static bool operator <(FinancialDate left, FinancialDate right) => left.Date < right.Date;
    public static bool operator >=(FinancialDate left, FinancialDate right) => left.Date >= right.Date;
    public static bool operator <=(FinancialDate left, FinancialDate right) => left.Date <= right.Date;
    public static bool operator ==(FinancialDate left, FinancialDate right) => left.Equals(right);
    public static bool operator !=(FinancialDate left, FinancialDate right) => !left.Equals(right);

    public bool Equals(FinancialDate other) => Date.Equals(other.Date);
    public override bool Equals(object? obj) => obj is FinancialDate other && Equals(other);
    public override int GetHashCode() => Date.GetHashCode();

    public int CompareTo(FinancialDate other) => Date.CompareTo(other.Date);
}
