using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

public class DebitCreditAndBalanceTests
{
    [Fact]
    public void DebitCredit_SignedMultiplier_ShouldReturnExpectedSign()
    {
        DebitCredit.Debit.ToSignedMultiplier().Should().Be(1);
        DebitCredit.Credit.ToSignedMultiplier().Should().Be(-1);
    }

    [Fact]
    public void DebitCredit_Opposite_ShouldInvertCorrectly()
    {
        DebitCredit.Debit.Opposite().Should().Be(DebitCredit.Credit);
        DebitCredit.Credit.Opposite().Should().Be(DebitCredit.Debit);
    }

    [Fact]
    public void AccountBalance_NetBalanceCalculation_ShouldAccuratelyDetermineDebtorOrCreditor()
    {
        var balance = AccountBalance.Zero(Currency.IRR);

        // Add 500,000 Debit
        balance = balance.AddDebit(500_000m);
        balance.DebitTotal.Should().Be(500_000m);
        balance.CreditTotal.Should().Be(0m);
        balance.NetBalance.Should().Be(500_000m);
        balance.IsDebtor.Should().BeTrue();
        balance.DominantSide.Should().Be(DebitCredit.Debit);

        // Add 200,000 Credit -> Net is 300,000 Debtor
        balance = balance.AddCredit(200_000m);
        balance.NetBalance.Should().Be(300_000m);
        balance.IsDebtor.Should().BeTrue();

        // Add 300,000 Credit -> Net is 0 (Balanced)
        balance = balance.AddCredit(300_000m);
        balance.IsBalanced.Should().BeTrue();
        balance.IsZero.Should().BeTrue();
        balance.DominantSide.Should().BeNull();

        // Add 100,000 Credit -> Net is -100,000 (Creditor)
        balance = balance.AddCredit(100_000m);
        balance.IsCreditor.Should().BeTrue();
        balance.DominantSide.Should().Be(DebitCredit.Credit);
    }
}

public class FinancialDateAndCalendarTests
{
    [Fact]
    public void FinancialDate_FromPersianDate_ShouldComputeCorrectGregorianDate()
    {
        // 1403/01/01 Persian corresponds to 2024-03-20 Gregorian (Norooz)
        var financialDate = FinancialDate.FromPersianDate(1403, 1, 1);

        financialDate.Date.Should().Be(new DateOnly(2024, 3, 20));
        financialDate.PersianYear.Should().Be(1403);
        financialDate.PersianMonth.Should().Be(1);
        financialDate.PersianDay.Should().Be(1);
        financialDate.ToPersianString().Should().Be("1403/01/01");
    }

    [Fact]
    public void FinancialDate_UtcConversion_ShouldProduceUtcKindDateTime()
    {
        var date = new FinancialDate(2025, 6, 15);

        var utcDateTime = date.ToUtcDateTime();
        var utcOffset = date.ToUtcDateTimeOffset();

        utcDateTime.Kind.Should().Be(DateTimeKind.Utc);
        utcOffset.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void FinancialDate_IsBetween_ShouldCorrectlyValidatePeriodRange()
    {
        var start = new FinancialDate(2024, 3, 20); // 1403/01/01
        var end = new FinancialDate(2025, 3, 20);   // 1403/12/30
        var midDate = new FinancialDate(2024, 7, 21); // 1403/04/31
        var outsideDate = new FinancialDate(2025, 4, 1);

        midDate.IsBetween(start, end).Should().BeTrue();
        outsideDate.IsBetween(start, end).Should().BeFalse();
    }
}
