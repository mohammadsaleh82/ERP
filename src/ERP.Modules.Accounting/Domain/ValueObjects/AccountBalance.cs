using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Domain.ValueObjects;

public sealed record AccountBalance
{
    public decimal DebitTotal { get; init; }
    public decimal CreditTotal { get; init; }
    public Currency Currency { get; init; }

    public decimal NetBalance => DebitTotal - CreditTotal;

    public bool IsBalanced => DebitTotal == CreditTotal;
    public bool IsDebtor => NetBalance > 0m;
    public bool IsCreditor => NetBalance < 0m;
    public bool IsZero => NetBalance == 0m;

    public DebitCredit? DominantSide =>
        IsDebtor ? DebitCredit.Debit : (IsCreditor ? DebitCredit.Credit : null);

    public AccountBalance(decimal debitTotal, decimal creditTotal, Currency currency)
    {
        DebitTotal = Math.Max(0m, debitTotal);
        CreditTotal = Math.Max(0m, creditTotal);
        Currency = currency ?? Currency.IRR;
    }

    public static AccountBalance Zero(Currency currency) => new(0m, 0m, currency);

    public AccountBalance AddDebit(decimal amount) =>
        new(checked(DebitTotal + Math.Max(0m, amount)), CreditTotal, Currency);

    public AccountBalance AddCredit(decimal amount) =>
        new(DebitTotal, checked(CreditTotal + Math.Max(0m, amount)), Currency);

    public AccountBalance AddEntry(DebitCredit side, decimal amount) =>
        side == DebitCredit.Debit ? AddDebit(amount) : AddCredit(amount);

    public override string ToString() =>
        $"بدهکار: {DebitTotal:N4} | بستانکار: {CreditTotal:N4} | مانده خالص: {Math.Abs(NetBalance):N4} ({DominantSide?.ToString() ?? "تراز"})";
}
