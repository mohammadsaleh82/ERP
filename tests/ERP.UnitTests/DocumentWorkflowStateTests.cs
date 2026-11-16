using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

/// <summary>
/// تست‌های ماشین وضعیت صریح چرخه عمر سند حسابداری.
/// قواعد انتقال وضعیت بر اساس سیاست تأیید شرکت باید کاملاً قابل پیش‌بینی و قابل تست باشند.
/// </summary>
public class DocumentWorkflowStateTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly Guid _fiscalPeriodId = Guid.NewGuid();

    private AccountingDocument CreateBalancedDraft(long number = 5001)
    {
        var doc = new AccountingDocument(
            _tenantId, _companyId, _fiscalYearId, _fiscalPeriodId,
            number, new DateOnly(2025, 6, 15), "سند تست چرخه عمر");

        doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 1_000_000m, "طرف بدهکار");
        doc.AddLine(Guid.NewGuid(), DebitCredit.Credit, 1_000_000m, "طرف بستانکار");
        return doc;
    }

    // ------------------------------------------------------------------
    // Submit For Approval
    // ------------------------------------------------------------------

    [Fact]
    public void Submit_FromDraft_ShouldMoveToPendingApproval()
    {
        var doc = CreateBalancedDraft();
        var actor = Guid.NewGuid().ToString();

        doc.SubmitForApproval(actor);

        doc.Status.Should().Be(DocumentStatus.PendingApproval);
        doc.SubmittedBy.Should().Be(actor);
        doc.SubmittedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(DocumentStatus.PendingApproval)]
    [InlineData(DocumentStatus.Approved)]
    [InlineData(DocumentStatus.Posted)]
    [InlineData(DocumentStatus.Reversed)]
    public void Submit_FromNonDraftStates_ShouldThrowInvalidTransition(DocumentStatus targetStatus)
    {
        var doc = CreateBalancedDraft();
        ForceStatus(doc, targetStatus);

        var action = () => doc.SubmitForApproval(Guid.NewGuid().ToString());

        action.Should().Throw<InvalidDocumentTransitionException>()
            .Which.CurrentStatus.Should().Be(targetStatus);
    }

    // ------------------------------------------------------------------
    // Approve / Reject
    // ------------------------------------------------------------------

    [Fact]
    public void Approve_TwoStepPolicy_FromPendingApproval_ShouldMoveToApproved()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");
        var actor = Guid.NewGuid().ToString();

        doc.Approve(DocumentApprovalPolicy.TwoStepApprovalRequired, actor);

        doc.Status.Should().Be(DocumentStatus.Approved);
        doc.ApprovedBy.Should().Be(actor);
        doc.ApprovedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Approve_ThreeStepPolicy_FirstApproval_ShouldMoveToReviewed()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");

        doc.Approve(DocumentApprovalPolicy.ThreeStepApprovalRequired, "expert");

        doc.Status.Should().Be(DocumentStatus.Reviewed);
        doc.ReviewedBy.Should().Be("expert");
        doc.ApprovedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Approve_ThreeStepPolicy_SecondApproval_ShouldMoveToApproved()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");
        doc.Approve(DocumentApprovalPolicy.ThreeStepApprovalRequired, "expert");

        doc.Approve(DocumentApprovalPolicy.ThreeStepApprovalRequired, "cfo");

        doc.Status.Should().Be(DocumentStatus.Approved);
        doc.ApprovedBy.Should().Be("cfo");
    }

    [Fact]
    public void Approve_DirectPolicy_DraftWithoutSubmit_ShouldNotBeAllowed()
    {
        var doc = CreateBalancedDraft();

        var action = () => doc.Approve(DocumentApprovalPolicy.DirectPostingAllowed, "user");

        action.Should().Throw<InvalidDocumentTransitionException>();
    }

    [Fact]
    public void Reject_FromPendingApproval_ShouldReturnToDraftWithReason()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");

        doc.Reject("approver", "شرح سند ناقص است");

        doc.Status.Should().Be(DocumentStatus.Draft);
        doc.RejectionReason.Should().Be("شرح سند ناقص است");
        doc.RejectedBy.Should().Be("approver");
    }

    [Fact]
    public void Reject_WithEmptyReason_ShouldThrow()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");

        var action = () => doc.Reject("approver", "  ");

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.EmptyRejectionReason");
    }

    [Fact]
    public void Reject_ApprovedDocument_ShouldThrow()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");
        doc.Approve(DocumentApprovalPolicy.TwoStepApprovalRequired, "approver");

        var action = () => doc.Reject("approver", "دلیل");

        action.Should().Throw<InvalidDocumentTransitionException>();
    }

    // ------------------------------------------------------------------
    // Post per policy
    // ------------------------------------------------------------------

    [Fact]
    public void Post_Draft_WithDirectPolicy_ShouldSucceed()
    {
        var doc = CreateBalancedDraft();

        doc.Post(DocumentApprovalPolicy.DirectPostingAllowed);

        doc.Status.Should().Be(DocumentStatus.Posted);
    }

    [Theory]
    [InlineData(DocumentApprovalPolicy.TwoStepApprovalRequired)]
    [InlineData(DocumentApprovalPolicy.ThreeStepApprovalRequired)]
    public void Post_Draft_WithoutApproval_ShouldThrow(DocumentApprovalPolicy policy)
    {
        var doc = CreateBalancedDraft();

        var action = () => doc.Post(policy);

        action.Should().Throw<InvalidDocumentTransitionException>();
    }

    [Theory]
    [InlineData(DocumentApprovalPolicy.DirectPostingAllowed)]
    [InlineData(DocumentApprovalPolicy.TwoStepApprovalRequired)]
    [InlineData(DocumentApprovalPolicy.ThreeStepApprovalRequired)]
    public void Post_ApprovedDocument_ShouldSucceedForAllPolicies(DocumentApprovalPolicy policy)
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");
        doc.Approve(policy, "approver");
        if (policy == DocumentApprovalPolicy.ThreeStepApprovalRequired)
        {
            doc.Status.Should().Be(DocumentStatus.Reviewed);
            doc.Approve(policy, "cfo"); // تأیید دوم در سیاست سه‌مرحله‌ای
        }

        doc.Status.Should().Be(DocumentStatus.Approved);
        doc.Post(policy);

        doc.Status.Should().Be(DocumentStatus.Posted);
    }

    // ------------------------------------------------------------------
    // Editability (سند قطعی قابل ویرایش و حذف مستقیم نیست)
    // ------------------------------------------------------------------

    [Fact]
    public void PostedDocument_ShouldRejectAddLineAndUpdateDelete()
    {
        var doc = CreateBalancedDraft();
        doc.Post(DocumentApprovalPolicy.DirectPostingAllowed);

        doc.IsEditable.Should().BeFalse();

        var addAction = () => doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 100m, "ردیف جدید");
        addAction.Should().Throw<ImmutableDocumentException>();

        var updateAction = () => doc.UpdateDraft(new DateOnly(2025, 7, 1), "شرح جدید");
        updateAction.Should().Throw<ImmutableDocumentException>();

        var removeAction = () => doc.RemoveLine(1);
        removeAction.Should().Throw<ImmutableDocumentException>();
    }

    [Fact]
    public void SubmittedDocument_ShouldRejectEditWhileInApprovalFlow()
    {
        var doc = CreateBalancedDraft();
        doc.SubmitForApproval("sub");

        doc.IsEditable.Should().BeFalse();

        var addAction = () => doc.AddLine(Guid.NewGuid(), DebitCredit.Debit, 100m, "ردیف");
        addAction.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.DocumentNotEditable");
    }

    [Fact]
    public void MarkPosted_ShouldRecordRealUserAndTimestamp()
    {
        var doc = CreateBalancedDraft();
        doc.Post(DocumentApprovalPolicy.DirectPostingAllowed);
        var actor = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        doc.MarkPosted(actor, now);

        doc.PostedBy.Should().Be(actor);
        doc.PostedAtUtc.Should().Be(now);
    }

    [Fact]
    public void MarkPosted_OnDraftDocument_ShouldThrow()
    {
        var doc = CreateBalancedDraft();

        var action = () => doc.MarkPosted("user", DateTimeOffset.UtcNow);

        action.Should().Throw<AccountingDomainException>()
            .Which.Code.Should().Be("Accounting.NotPostedState");
    }

    // ------------------------------------------------------------------
    // Reverse
    // ------------------------------------------------------------------

    [Fact]
    public void CreateReversal_FromPostedDocument_ShouldMirrorLinesAndReverseStatus()
    {
        var doc = CreateBalancedDraft();
        doc.Post(DocumentApprovalPolicy.DirectPostingAllowed);

        var reversal = doc.CreateReversal(9001, new DateOnly(2025, 6, 16), "برگشت کامل سند", _fiscalPeriodId);

        doc.Status.Should().Be(DocumentStatus.Reversed);
        doc.ReversedDocumentId.Should().Be(reversal.Id);
        reversal.Status.Should().Be(DocumentStatus.Posted);
        reversal.TotalDebit.Should().Be(doc.TotalDebit);
        reversal.TotalCredit.Should().Be(doc.TotalCredit);
        reversal.Lines.Should().HaveCount(2);
        reversal.Lines[0].DebitCredit.Should().Be(DebitCredit.Credit);
        reversal.Lines[1].DebitCredit.Should().Be(DebitCredit.Debit);
    }

    [Fact]
    public void CreateReversal_FromDraftDocument_ShouldThrow()
    {
        var doc = CreateBalancedDraft();

        var action = () => doc.CreateReversal(9002, new DateOnly(2025, 6, 16), "برگشت", _fiscalPeriodId);

        action.Should().Throw<InvalidDocumentTransitionException>();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>تنظیم مستقیم وضعیت (فقط برای شبیه‌سازی داده از پیش موجود در تست‌ها).</summary>
    private static void ForceStatus(AccountingDocument doc, DocumentStatus status) => doc.Status = status;
}
