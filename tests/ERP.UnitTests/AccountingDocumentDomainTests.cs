using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Events;
using ERP.Modules.Accounting.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

public class AccountingDocumentDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly Guid _fiscalPeriodId = Guid.NewGuid();

    [Fact]
    public void Document_WhenUnbalanced_PostShouldThrowUnbalancedDocumentException()
    {
        var doc = new AccountingDocument(
            _tenantId,
            _companyId,
            _fiscalYearId,
            _fiscalPeriodId,
            documentNumber: 1001,
            documentDate: new DateOnly(2024, 5, 10),
            description: "سند خرید ملزومات اداری");

        var cashAccountId = Guid.NewGuid();
        var expenseAccountId = Guid.NewGuid();

        doc.AddLine(expenseAccountId, DebitCredit.Debit, 1_000_000m, "هزینه ملزومات");
        doc.AddLine(cashAccountId, DebitCredit.Credit, 950_000m, "صندوق"); // Unbalanced by 50,000!

        doc.IsBalanced.Should().BeFalse();
        doc.ImbalanceAmount.Should().Be(50_000m);

        var action = () => doc.Post();

        action.Should().Throw<UnbalancedDocumentException>()
            .Which.Difference.Should().Be(50_000m);
    }

    [Fact]
    public void Document_WhenBalanced_PostShouldSucceedAndEmitDomainEvent()
    {
        var doc = new AccountingDocument(
            _tenantId,
            _companyId,
            _fiscalYearId,
            _fiscalPeriodId,
            documentNumber: 1002,
            documentDate: new DateOnly(2024, 5, 10),
            description: "سند پرداخت تنخواه");

        var bankAccountId = Guid.NewGuid();
        var pettyCashAccountId = Guid.NewGuid();

        doc.AddLine(pettyCashAccountId, DebitCredit.Debit, 5_000_000m, "شارژ تنخواه");
        doc.AddLine(bankAccountId, DebitCredit.Credit, 5_000_000m, "بانک صادرات");

        doc.IsBalanced.Should().BeTrue();
        doc.Status.Should().Be(DocumentStatus.Draft);

        doc.Post();

        doc.Status.Should().Be(DocumentStatus.Posted);
        doc.IsImmutable.Should().BeTrue();

        var postedEvent = doc.DomainEvents.OfType<AccountingDocumentPostedEvent>().FirstOrDefault();
        postedEvent.Should().NotBeNull();
        postedEvent!.TotalAmount.Should().Be(5_000_000m);
        postedEvent.DocumentNumber.Should().Be(1002);
    }

    [Fact]
    public void Document_WhenPosted_AttemptingToModifyLinesShouldThrowImmutableDocumentException()
    {
        var doc = new AccountingDocument(
            _tenantId,
            _companyId,
            _fiscalYearId,
            _fiscalPeriodId,
            documentNumber: 1003,
            documentDate: new DateOnly(2024, 5, 10),
            description: "سند قطعی‌شده");

        var acc1 = Guid.NewGuid();
        var acc2 = Guid.NewGuid();
        doc.AddLine(acc1, DebitCredit.Debit, 1_000_000m, "بدهکار");
        doc.AddLine(acc2, DebitCredit.Credit, 1_000_000m, "بستانکار");
        doc.Post();

        // 1. Attempt to add line on posted document
        var addAction = () => doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 500_000m, "سطر غیرمجاز");
        addAction.Should().Throw<ImmutableDocumentException>();

        // 2. Attempt to remove line on posted document
        var removeAction = () => doc.RemoveLine(1);
        removeAction.Should().Throw<ImmutableDocumentException>();
    }

    [Fact]
    public void Document_CreateReversal_ShouldInvertDebitCreditAndEmitReversedEvent()
    {
        var originalDoc = new AccountingDocument(
            _tenantId,
            _companyId,
            _fiscalYearId,
            _fiscalPeriodId,
            documentNumber: 1004,
            documentDate: new DateOnly(2024, 5, 10),
            description: "سند اشتباه نیازمند معکوس‌سازی");

        var accExpense = Guid.NewGuid();
        var accBank = Guid.NewGuid();

        originalDoc.AddLine(accExpense, DebitCredit.Debit, 2_000_000m, "هزینه بیمه");
        originalDoc.AddLine(accBank, DebitCredit.Credit, 2_000_000m, "بانک ملت");
        originalDoc.Post();

        // Perform Reversal (Storno)
        var reversalDoc = originalDoc.CreateReversal(
            newDocumentNumber: 1005,
            reversalDate: new DateOnly(2024, 5, 11),
            reversalDescription: "سند اصلاحی و برگشت سند ۱۰-۰۴",
            fiscalPeriodId: _fiscalPeriodId);

        // Assert Original Document state
        originalDoc.Status.Should().Be(DocumentStatus.Reversed);
        originalDoc.ReversedDocumentId.Should().Be(reversalDoc.Id);

        // Assert Reversal Document state
        reversalDoc.DocumentType.Should().Be(DocumentType.Reversal);
        reversalDoc.DocumentNumber.Should().Be(1005);
        reversalDoc.Lines.Should().HaveCount(2);

        // Line 1: Original was Debit 2M -> Reversal must be Credit 2M
        var line1 = reversalDoc.Lines.First(l => l.AccountId == accExpense);
        line1.DebitCredit.Should().Be(DebitCredit.Credit);
        line1.CreditAmount.Should().Be(2_000_000m);
        line1.DebitAmount.Should().Be(0m);

        // Line 2: Original was Credit 2M -> Reversal must be Debit 2M
        var line2 = reversalDoc.Lines.First(l => l.AccountId == accBank);
        line2.DebitCredit.Should().Be(DebitCredit.Debit);
        line2.DebitAmount.Should().Be(2_000_000m);
        line2.CreditAmount.Should().Be(0m);

        // Reversal doc is automatically balanced
        reversalDoc.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void Document_WithInsufficientLines_CannotBePosted()
    {
        var doc = new AccountingDocument(
            _tenantId,
            _companyId,
            _fiscalYearId,
            _fiscalPeriodId,
            documentNumber: 1006,
            documentDate: new DateOnly(2024, 5, 10),
            description: "سند تک‌سطری");

        doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 1_000_000m, "فقط بدهکار");

        var action = () => doc.Post();

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*حداقل شامل دو سطر*");
    }
}
