using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.Services;

/// <summary>
/// سیاست نمایش مانده حساب‌ها (Pure و Unit-Test پذیر).
/// قرارداد علامت‌گذاری: مانده علامت‌دار = جمع بدهکار − جمع بستانکار؛
/// مقدار مثبت یعنی مانده بدهکار و مقدار منفی یعنی مانده بستانکار.
/// برای هر حساب با توجه به ماهیت عادی (Normal Balance):
///   - مانده هم‌جهت با ماهیت → طبیعی (Normal)
///   - مانده خلاف جهت ماهیت → «مانده منفی/معکوس» (Abnormal) با پرچم صریح و جهت نمایش مخالف ماهیت
/// مقدار نمایشی همیشه قدرمطلق مانده است تا هیچ علامتی در گزارش گم نشود.
/// </summary>
public static class BalanceCalculator
{
    public static LedgerBalanceDirection ResolveDirection(decimal signedBalance) => signedBalance switch
    {
        > 0m => LedgerBalanceDirection.Debtor,
        < 0m => LedgerBalanceDirection.Creditor,
        _ => LedgerBalanceDirection.None
    };

    public static decimal ResolveDisplayBalance(decimal signedBalance) => Math.Abs(signedBalance);

    /// <summary>آیا مانده خلاف جهت ماهیت عادی حساب است؟ (مانده صفر هرگز غیرعادی نیست)</summary>
    public static bool IsAbnormal(NormalBalance normalBalance, decimal signedBalance) =>
        signedBalance switch
        {
            > 0m when normalBalance == NormalBalance.Credit => true,  // حساب بستانکار با مانده بدهکار
            < 0m when normalBalance == NormalBalance.Debit => true,   // حساب بدهکار با مانده بستانکار
            _ => false
        };

    public static AccountBalanceResultDto ToBalanceResult(
        Guid accountId,
        string fullCode,
        string name,
        NormalBalance normalBalance,
        decimal openingBalance,
        decimal periodDebit,
        decimal periodCredit)
    {
        var closingBalance = openingBalance + periodDebit - periodCredit;

        return new AccountBalanceResultDto(
            accountId,
            fullCode,
            name,
            normalBalance,
            openingBalance,
            periodDebit,
            periodCredit,
            closingBalance,
            ResolveDirection(openingBalance),
            ResolveDirection(closingBalance),
            ResolveDisplayBalance(openingBalance),
            ResolveDisplayBalance(closingBalance),
            IsAbnormal(normalBalance, openingBalance),
            IsAbnormal(normalBalance, closingBalance));
    }
}
