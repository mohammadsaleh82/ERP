using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

/// <summary>
/// سناریوهای حیاتی قوانین Double Entry در سطح دامنه (سناریوهای ۱ تا ۷ پرامپت).
/// </summary>
public class VoucherDoubleEntryRulesTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly Guid _fiscalPeriodId = Guid.NewGuid();

    private AccountingDocument CreateDocument(long number = 1001) => new(
        _tenantId, _companyId, _fiscalYearId, _fiscalPeriodId,
        number, new DateOnly(2025, 5, 10), "سند تست قوانین دوبل");

    /// <summary>سناریو ۱: سند متوازن قابل قطعی‌سازی است.</summary>
    [Fact]
    public void BalancedDocument_Post_ShouldSucceed()
    {
        var doc = CreateDocument();
        doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 2_500_000.5m, "خرید کالا");
        doc.AddLine(Guid.NewGuid(), DebitCredit.Credit, 2_500_000.5m, "پرداخت به تامین‌کننده");

        doc.IsBalanced.Should().BeTrue();

        doc.Post();

        doc.Status.Should().Be(DocumentStatus.Posted);
        doc.TotalDebit.Should().Be(doc.TotalCredit);
    }

    /// <summary>سناریو ۲: سند نامتوازن — حتی اختلاف یک واحد کوچک‌تر از Precision مجاز قطعی نمی‌شود.</summary>
    [Theory]
    [InlineData(500_000d)]
    [InlineData(0.0001d)]
    public void UnbalancedDocument_Post_ShouldThrowUnbalancedException(double imbalance)
    {
        var doc = CreateDocument();
        doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 1_000_000m, "بدهکار");
        doc.AddLine(Guid.NewGuid(), DebitCredit.Credit, 1_000_000m - (decimal)imbalance, "بستانکار");

        doc.IsBalanced.Should().BeFalse();
        doc.ImbalanceAmount.Should().Be((decimal)imbalance);

        var action = () => doc.Post();

        action.Should().Throw<UnbalancedDocumentException>()
            .Which.Difference.Should().Be((decimal)imbalance);
    }

    /// <summary>سناریو ۳: سند بدون ردیف معتبر قطعی نمی‌شود.</summary>
    [Fact]
    public void DocumentWithoutLines_Post_ShouldThrow()
    {
        var doc = CreateDocument();

        var action = () => doc.Post();

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.InsufficientLines");
    }

    /// <summary>سناریو ۴: سند با ردیف صفر مجاز نیست.</summary>
    [Fact]
    public void ZeroAmountLine_ShouldThrowOnAdd()
    {
        var doc = CreateDocument();

        var action = () => doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 0m, "ردیف بدون مبلغ");

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.InvalidLineAmount");
    }

    /// <summary>
    /// سناریو ۵: بدهکار و بستانکار هم‌زمان در یک ردیف — با طراحی DebitCredit یک ردیف فقط یک سمت دارد؛
    /// کنترل پایگاه‌داده‌ای/موتور، داده مخدوش وارد‌شده از مسیرهای دیگر را می‌گیرد (تست موتور در Integration).
    /// </summary>
    [Fact]
    public void Line_MustHaveExactlyOneSide_AtConstruction()
    {
        var doc = CreateDocument();
        var line = doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 750_000m, "فقط بدهکار");

        (line.DebitAmount > 0m && line.CreditAmount > 0m).Should().BeFalse();
        line.DebitAmount.Should().Be(750_000m);
        line.CreditAmount.Should().Be(0m);
    }

    /// <summary>سناریو ۶ (دامنه): حساب غیرفعال/غیرقابل‌ثبت قابل پذیرش سند نیست.</summary>
    [Fact]
    public void InactiveAccount_EnsureCanPost_ShouldThrow()
    {
        var account = Account.CreateRoot(_tenantId, _companyId, "10", "دارایی‌های جاری", AccountNature.Asset);
        var postingAccount = account.CreateChild("01", "صندوق", allowPosting: true);
        postingAccount.Deactivate();

        var action = () => postingAccount.EnsureCanPost();

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.AccountInactive");
    }

    /// <summary>سناریو ۶ (دامنه): حساب تجمیعی/کنترلی بدون مجوز ثبت، قابل پذیرش سند نیست.</summary>
    [Fact]
    public void NonPostingAccount_EnsureCanPost_ShouldThrow()
    {
        var account = Account.CreateRoot(_tenantId, _companyId, "10", "دارایی‌های جاری", AccountNature.Asset);

        var action = () => account.EnsureCanPost();

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.AccountDirectPostingForbidden");
    }

    /// <summary>سناریو ۷ (دامنه): حسابِ شرکت دیگر در سطح والد/فرزند ممنوع است؛ کنترل سند در موتور انجام می‌شود.</summary>
    [Fact]
    public void CrossCompanyParentAccount_ShouldThrow()
    {
        var account = Account.CreateRoot(_tenantId, _companyId, "10", "دارایی", AccountNature.Asset);
        var foreignAccount = Account.CreateRoot(_tenantId, Guid.NewGuid(), "20", "بدهی شرکت دیگر", AccountNature.Liability);

        var action = () => account.ChangeParent(foreignAccount);

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.CrossCompanyParentForbidden");
    }

    /// <summary>قانون مکمل: ردیف با نرخ تسعیر غیرمثبت مجاز نیست.</summary>
    [Fact]
    public void NonPositiveExchangeRate_ShouldThrow()
    {
        var doc = CreateDocument();

        var action = () => doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 100m, "ردیف ارزی",
            foreignCurrencyCode: "USD", foreignCurrencyAmount: 10m, exchangeRate: 0m);

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.InvalidExchangeRate");
    }

    /// <summary>قانون مکمل: مبلغ ارزی بدون کد ارز مجاز نیست.</summary>
    [Fact]
    public void ForeignAmountWithoutCurrency_ShouldThrow()
    {
        var doc = CreateDocument();

        var action = () => doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 100m, "ردیف ارزی",
            foreignCurrencyAmount: 10m);

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.MissingForeignCurrency");
    }

    /// <summary>تقارن ردیف‌ها: حذف ردیف باید شماره‌ها و جمع‌ها را بازسازی کند.</summary>
    [Fact]
    public void RemoveLine_ShouldReSequenceAndRecalculateTotals()
    {
        var doc = CreateDocument();
        doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 300m, "الف");
        doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 200m, "ب");
        doc.AddLine(Guid.NewGuid(), DebitCredit.Credit, 500m, "ج");

        doc.RemoveLine(2);

        doc.Lines.Should().HaveCount(2);
        doc.Lines.Select(l => l.LineNumber).Should().BeEquivalentTo([1, 2]);
        doc.TotalDebit.Should().Be(300m);
        doc.TotalCredit.Should().Be(500m);
    }
}
