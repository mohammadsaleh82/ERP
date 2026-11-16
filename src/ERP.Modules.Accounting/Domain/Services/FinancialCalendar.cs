using System.Globalization;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Services;

public interface IFinancialCalendarService
{
    (DateOnly StartDate, DateOnly EndDate) GetYearBounds(FiscalCalendarType calendarType, int yearNumber);
    int GetDaysInMonth(FiscalCalendarType calendarType, int yearNumber, int month);
    List<(int PeriodNumber, string Name, DateOnly StartDate, DateOnly EndDate)> GenerateStandardPeriods(
        FiscalCalendarType calendarType,
        int yearNumber,
        PeriodGenerationType generationType);

    (int Year, int Month, int Day) ToPersianDate(DateOnly date);
    DateOnly ToGregorianDate(int pYear, int pMonth, int pDay);
    string FormatDate(DateOnly date, FiscalCalendarType calendarType);
    bool IsLeapYear(FiscalCalendarType calendarType, int yearNumber);
}

public class FinancialCalendarService : IFinancialCalendarService
{
    private static readonly PersianCalendar _persianCalendar = new();

    private static readonly string[] _persianMonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    ];

    private static readonly string[] _persianQuarterNames =
    [
        "سه ماهه اول (بهار)",
        "سه ماهه دوم (تابستان)",
        "سه ماهه سوم (پاییز)",
        "سه ماهه چهارم (زمستان)"
    ];

    private static readonly string[] _gregorianMonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    private static readonly string[] _gregorianQuarterNames =
    [
        "Quarter 1 (Q1)",
        "Quarter 2 (Q2)",
        "Quarter 3 (Q3)",
        "Quarter 4 (Q4)"
    ];

    public (DateOnly StartDate, DateOnly EndDate) GetYearBounds(FiscalCalendarType calendarType, int yearNumber)
    {
        if (calendarType == FiscalCalendarType.Persian)
        {
            if (yearNumber < 1300 || yearNumber > 1500)
                throw new AccountingDomainException("Accounting.InvalidPersianYear", $"سال شمسی '{yearNumber}' خارج از محدوده مجاز (۱۳۰۰ تا ۱۵۰۰) است.");

            var startDate = ToGregorianDate(yearNumber, 1, 1);
            var daysInEsfand = IsLeapYear(FiscalCalendarType.Persian, yearNumber) ? 30 : 29;
            var endDate = ToGregorianDate(yearNumber, 12, daysInEsfand);

            return (startDate, endDate);
        }
        else
        {
            if (yearNumber < 1900 || yearNumber > 2150)
                throw new AccountingDomainException("Accounting.InvalidGregorianYear", $"سال میلادی '{yearNumber}' خارج از محدوده مجاز (۱۹۰۰ تا ۲۱۵۰) است.");

            var startDate = new DateOnly(yearNumber, 1, 1);
            var endDate = new DateOnly(yearNumber, 12, 31);

            return (startDate, endDate);
        }
    }

    public int GetDaysInMonth(FiscalCalendarType calendarType, int yearNumber, int month)
    {
        if (month < 1 || month > 12)
            throw new AccountingDomainException("Accounting.InvalidMonth", "شماره ماه باید بین ۱ تا ۱۲ باشد.");

        if (calendarType == FiscalCalendarType.Persian)
        {
            if (month <= 6) return 31;
            if (month <= 11) return 30;
            return IsLeapYear(FiscalCalendarType.Persian, yearNumber) ? 30 : 29;
        }

        return DateTime.DaysInMonth(yearNumber, month);
    }

    public bool IsLeapYear(FiscalCalendarType calendarType, int yearNumber)
    {
        if (calendarType == FiscalCalendarType.Persian)
        {
            return _persianCalendar.IsLeapYear(yearNumber);
        }

        return DateTime.IsLeapYear(yearNumber);
    }

    public List<(int PeriodNumber, string Name, DateOnly StartDate, DateOnly EndDate)> GenerateStandardPeriods(
        FiscalCalendarType calendarType,
        int yearNumber,
        PeriodGenerationType generationType)
    {
        var periods = new List<(int PeriodNumber, string Name, DateOnly StartDate, DateOnly EndDate)>();

        if (generationType == PeriodGenerationType.Monthly)
        {
            for (var m = 1; m <= 12; m++)
            {
                var days = GetDaysInMonth(calendarType, yearNumber, m);
                DateOnly start;
                DateOnly end;
                string name;

                if (calendarType == FiscalCalendarType.Persian)
                {
                    start = ToGregorianDate(yearNumber, m, 1);
                    end = ToGregorianDate(yearNumber, m, days);
                    name = _persianMonthNames[m - 1];
                }
                else
                {
                    start = new DateOnly(yearNumber, m, 1);
                    end = new DateOnly(yearNumber, m, days);
                    name = _gregorianMonthNames[m - 1];
                }

                periods.Add((m, name, start, end));
            }
        }
        else if (generationType == PeriodGenerationType.Quarterly)
        {
            for (var q = 1; q <= 4; q++)
            {
                var startMonth = (q - 1) * 3 + 1;
                var endMonth = q * 3;
                var endDays = GetDaysInMonth(calendarType, yearNumber, endMonth);

                DateOnly start;
                DateOnly end;
                string name;

                if (calendarType == FiscalCalendarType.Persian)
                {
                    start = ToGregorianDate(yearNumber, startMonth, 1);
                    end = ToGregorianDate(yearNumber, endMonth, endDays);
                    name = _persianQuarterNames[q - 1];
                }
                else
                {
                    start = new DateOnly(yearNumber, startMonth, 1);
                    end = new DateOnly(yearNumber, endMonth, endDays);
                    name = _gregorianQuarterNames[q - 1];
                }

                periods.Add((q, name, start, end));
            }
        }
        else
        {
            throw new AccountingDomainException("Accounting.InvalidPeriodGenerationType", "تولید خودکار فقط برای دوره‌های ماهانه و فصلی پشتیبانی می‌شود.");
        }

        return periods;
    }

    public (int Year, int Month, int Day) ToPersianDate(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        var y = _persianCalendar.GetYear(dt);
        var m = _persianCalendar.GetMonth(dt);
        var d = _persianCalendar.GetDayOfMonth(dt);
        return (y, m, d);
    }

    public DateOnly ToGregorianDate(int pYear, int pMonth, int pDay)
    {
        var dt = _persianCalendar.ToDateTime(pYear, pMonth, pDay, 0, 0, 0, 0);
        return DateOnly.FromDateTime(dt);
    }

    public string FormatDate(DateOnly date, FiscalCalendarType calendarType)
    {
        if (calendarType == FiscalCalendarType.Persian)
        {
            var (y, m, d) = ToPersianDate(date);
            return $"{y:D4}/{m:D2}/{d:D2}";
        }

        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
