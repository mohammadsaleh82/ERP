using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.ValueObjects;

public sealed record Currency
{
    public string Code { get; }
    public string Name { get; }
    public string Symbol { get; }
    public int DecimalPlaces { get; }

    public static readonly Currency IRR = new("IRR", "ریال ایران", "ریال", 0);
    public static readonly Currency TOMAN = new("IRT", "تومان ایران", "تومان", 0);
    public static readonly Currency USD = new("USD", "دلار آمریکا", "$", 2);
    public static readonly Currency EUR = new("EUR", "یورو", "€", 2);
    public static readonly Currency GBP = new("GBP", "پوند انگلیس", "£", 2);
    public static readonly Currency AED = new("AED", "درهم امارات", "AED", 2);

    private Currency(string code, string name, string symbol, int decimalPlaces)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 3)
            throw new AccountingDomainException("Accounting.InvalidCurrencyCode", "کد ارز باید یک کد ۳ حرفی استاندارد ISO 4217 باشد.");

        Code = code.Trim().ToUpperInvariant();
        Name = name;
        Symbol = symbol;
        DecimalPlaces = decimalPlaces < 0 || decimalPlaces > 6 ? 2 : decimalPlaces;
    }

    public static Currency Create(string code, string name, string symbol, int decimalPlaces) =>
        new(code, name, symbol, decimalPlaces);

    public static Currency FromCode(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "IRR" => IRR,
            "IRT" => TOMAN,
            "USD" => USD,
            "EUR" => EUR,
            "GBP" => GBP,
            "AED" => AED,
            _ => new Currency(normalized ?? "IRR", normalized ?? "Unknown", normalized ?? "¤", 2)
        };
    }

    public override string ToString() => Code;
}

public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public const int DefaultScale = 4;
    public const MidpointRounding DefaultRounding = MidpointRounding.ToEven;

    public decimal Amount { get; }
    public Currency Currency { get; }

    public bool IsZero => Amount == 0m;
    public bool IsPositive => Amount > 0m;
    public bool IsNegative => Amount < 0m;

    public Money(decimal amount, Currency currency)
    {
        Currency = currency ?? Currency.IRR;
        Amount = amount;
    }

    public Money(decimal amount, string currencyCode)
        : this(amount, Currency.FromCode(currencyCode))
    {
    }

    public static Money Zero(Currency currency) => new(0m, currency);
    public static Money Zero(string currencyCode) => new(0m, Currency.FromCode(currencyCode));

    public static Money FromRials(decimal amount) => new(amount, Currency.IRR);

    public Money Round(int? decimalPlaces = null, MidpointRounding rounding = DefaultRounding)
    {
        var places = decimalPlaces ?? Currency.DecimalPlaces;
        return new Money(Math.Round(Amount, places, rounding), Currency);
    }

    public Money ConvertTo(Currency targetCurrency, decimal exchangeRate, MidpointRounding rounding = DefaultRounding)
    {
        if (exchangeRate <= 0)
            throw new AccountingDomainException("Accounting.InvalidExchangeRate", "نرخ تسعیر ارز باید عددی مثبت باشد.");

        if (Currency == targetCurrency)
            return this;

        var converted = checked(Amount * exchangeRate);
        var rounded = Math.Round(converted, targetCurrency.DecimalPlaces, rounding);
        return new Money(rounded, targetCurrency);
    }

    public Money Abs() => new(Math.Abs(Amount), Currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount + right.Amount), left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount - right.Amount), left.Currency);
    }

    public static Money operator -(Money money) =>
        new(checked(-money.Amount), money.Currency);

    public static Money operator *(Money money, decimal factor) =>
        new(checked(money.Amount * factor), money.Currency);

    public static Money operator *(decimal factor, Money money) =>
        money * factor;

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("تقسیم مبلغ پولی بر صفر مجاز نمی‌باشد.");
        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    public bool Equals(Money other) =>
        Amount == other.Amount && Equals(Currency, other.Currency);

    public override bool Equals(object? obj) =>
        obj is Money other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Amount, Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() =>
        $"{Amount.ToString("N" + Currency.DecimalPlaces)} {Currency.Symbol}";

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency && a.Currency.Code != b.Currency.Code)
        {
            throw new CurrencyMismatchException(a.Currency.Code, b.Currency.Code);
        }
    }
}
