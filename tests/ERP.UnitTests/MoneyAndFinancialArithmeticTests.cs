using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

public class MoneyAndFinancialArithmeticTests
{
    [Fact]
    public void Money_Addition_WithSameCurrency_ShouldSucceed()
    {
        var m1 = new Money(150_000m, Currency.IRR);
        var m2 = new Money(250_000m, Currency.IRR);

        var result = m1 + m2;

        result.Amount.Should().Be(400_000m);
        result.Currency.Should().Be(Currency.IRR);
    }

    [Fact]
    public void Money_Subtraction_WithSameCurrency_ShouldSucceed()
    {
        var m1 = new Money(500_000m, Currency.IRR);
        var m2 = new Money(150_000m, Currency.IRR);

        var result = m1 - m2;

        result.Amount.Should().Be(350_000m);
        result.Currency.Should().Be(Currency.IRR);
    }

    [Fact]
    public void Money_Addition_WithDifferentCurrencies_ShouldThrowCurrencyMismatchException()
    {
        var irr = new Money(1_000_000m, Currency.IRR);
        var usd = new Money(100m, Currency.USD);

        var action = () => { var _ = irr + usd; };

        action.Should().Throw<CurrencyMismatchException>()
            .WithMessage("*نرخ تسعیر ارز*");
    }

    [Fact]
    public void Money_MultiplicationAndDivision_ShouldCalculateCorrectly()
    {
        var price = new Money(25.50m, Currency.USD);

        var total = price * 4m;
        var perUnit = total / 2m;

        total.Amount.Should().Be(102.00m);
        perUnit.Amount.Should().Be(51.00m);
    }

    [Fact]
    public void Money_DivisionByZero_ShouldThrowDivideByZeroException()
    {
        var money = new Money(100m, Currency.USD);

        var action = () => { var _ = money / 0m; };

        action.Should().Throw<DivideByZeroException>();
    }

    [Theory]
    [InlineData(10.505, 2, 10.50)] // Bank rounding: 0 is even, so rounds to 10.50
    [InlineData(10.515, 2, 10.52)] // Bank rounding: 1 is odd, so rounds to 10.52
    [InlineData(10.525, 2, 10.52)] // Bank rounding: 2 is even, so rounds to 10.52
    public void Money_BankersRounding_ShouldAvoidCumulativeBias(decimal amount, int places, decimal expected)
    {
        var money = new Money(amount, Currency.USD);

        var rounded = money.Round(places, MidpointRounding.ToEven);

        rounded.Amount.Should().Be(expected);
    }

    [Fact]
    public void Money_CurrencyConversion_WithValidExchangeRate_ShouldCalculateAccurately()
    {
        var usd = new Money(100m, Currency.USD);
        var exchangeRate = 700_000m; // 1 USD = 700,000 IRR

        var converted = usd.ConvertTo(Currency.IRR, exchangeRate);

        converted.Currency.Should().Be(Currency.IRR);
        converted.Amount.Should().Be(70_000_000m);
    }

    [Fact]
    public void Money_OverflowProtection_ShouldThrowOverflowException()
    {
        var maxMoney = new Money(decimal.MaxValue, Currency.IRR);
        var extra = new Money(1m, Currency.IRR);

        var action = () => { var _ = maxMoney + extra; };

        action.Should().Throw<OverflowException>();
    }
}
