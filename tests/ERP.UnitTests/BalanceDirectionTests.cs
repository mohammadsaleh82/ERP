using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

/// <summary>
/// تست‌های سیاست نمایش مانده حساب‌ها (Pure Logic).
/// قرارداد: مانده علامت‌دار = بدهکار − بستانکار؛ نمایش = قدرمطلق + جهت صریح؛
/// مانده خلاف ماهیت عادی حساب با پرچم IsAbnormalBalance مشخص می‌شود.
/// </summary>
public class BalanceDirectionTests
{
    [Theory]
    [InlineData(1_000_000d, LedgerBalanceDirection.Debtor, 1_000_000d)]
    [InlineData(-750_000d, LedgerBalanceDirection.Creditor, 750_000d)]
    [InlineData(0d, LedgerBalanceDirection.None, 0d)]
    public void ResolveDirection_ShouldFollowSignConvention(double signed, LedgerBalanceDirection direction, double display)
    {
        var value = (decimal)signed;

        BalanceCalculator.ResolveDirection(value).Should().Be(direction);
        BalanceCalculator.ResolveDisplayBalance(value).Should().Be((decimal)display);
    }

    [Fact]
    public void DebitNatureAccount_WithCreditBalance_ShouldBeAbnormal()
    {
        // حساب دارایی (ماهیت بدهکار) با مانده بستانکار ۳۰۰ = مانده منفی/معکوس
        BalanceCalculator.IsAbnormal(NormalBalance.Debit, -300m).Should().BeTrue();
        BalanceCalculator.IsAbnormal(NormalBalance.Debit, 300m).Should().BeFalse();
    }

    [Fact]
    public void CreditNatureAccount_WithDebitBalance_ShouldBeAbnormal()
    {
        // حساب بدهی (ماهیت بستانکار) با مانده بدهکار ۵۰۰ = مانده معکوس
        BalanceCalculator.IsAbnormal(NormalBalance.Credit, 500m).Should().BeTrue();
        BalanceCalculator.IsAbnormal(NormalBalance.Credit, -500m).Should().BeFalse();
    }

    [Fact]
    public void ZeroBalance_ShouldNeverBeAbnormal()
    {
        BalanceCalculator.IsAbnormal(NormalBalance.Debit, 0m).Should().BeFalse();
        BalanceCalculator.IsAbnormal(NormalBalance.Credit, 0m).Should().BeFalse();
    }

    [Fact]
    public void ToBalanceResult_ClosingEqualsOpeningPlusDebitMinusCredit()
    {
        var result = BalanceCalculator.ToBalanceResult(
            Guid.NewGuid(), "10", "دارایی", NormalBalance.Debit,
            openingBalance: 100m, periodDebit: 250m, periodCredit: 80m);

        result.ClosingBalance.Should().Be(270m);
        result.ClosingDirection.Should().Be(LedgerBalanceDirection.Debtor);
        result.ClosingDisplayBalance.Should().Be(270m);
        result.IsAbnormalClosingBalance.Should().BeFalse();
    }

    [Fact]
    public void ToBalanceResult_ReverseBalance_ShouldPreserveSignAndFlag()
    {
        // حساب بستانکار (بدهی) با مانده بدهکار → خلاف ماهیت عادی (معکوس)
        var payable = BalanceCalculator.ToBalanceResult(
            Guid.NewGuid(), "20", "بدهی", NormalBalance.Credit,
            openingBalance: 0m, periodDebit: 900m, periodCredit: 200m);

        payable.ClosingBalance.Should().Be(700m, "علامت مانده هرگز حذف نمی‌شود");
        payable.ClosingDirection.Should().Be(LedgerBalanceDirection.Debtor);
        payable.ClosingDisplayBalance.Should().Be(700m);
        payable.IsAbnormalClosingBalance.Should().BeTrue("مانده بدهکار روی حساب بستانکار غیرعادی است");

        // حساب بدهکار (دارایی) با مانده بستانکار → خلاف ماهیت عادی (معکوس)
        var asset = BalanceCalculator.ToBalanceResult(
            Guid.NewGuid(), "10", "دارایی", NormalBalance.Debit,
            openingBalance: 0m, periodDebit: 200m, periodCredit: 900m);

        asset.ClosingBalance.Should().Be(-700m);
        asset.ClosingDirection.Should().Be(LedgerBalanceDirection.Creditor);
        asset.ClosingDisplayBalance.Should().Be(700m);
        asset.IsAbnormalClosingBalance.Should().BeTrue("مانده بستانکار روی حساب بدهکار غیرعادی است");
    }
}
