using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Services;

/// <summary>
/// ماشین وضعیت صریح چرخه عمر سند حسابداری.
/// تمام انتقال‌های وضعیت فقط از طریق این ماشین وضعیت مجازند و هر انتقال غیرمجاز
/// با <see cref="InvalidDocumentTransitionException"/> رد می‌شود.
/// قواعد انتقال بر اساس سیاست تأیید شرکت (Approval Policy) پارامتریزه شده‌اند.
/// </summary>
public static class AccountingDocumentWorkflow
{
    /// <summary>
    /// ارسال سند پیش‌نویس به جریان تأیید (Submit For Approval).
    /// </summary>
    public static bool CanSubmit(DocumentStatus currentStatus) => currentStatus == DocumentStatus.Draft;

    /// <summary>
    /// تأیید سند؛ در سیاست سه‌مرحله‌ای نخستین تأیید فقط به مرحله «بررسی‌شده» می‌رسد.
    /// </summary>
    public static bool CanApprove(DocumentStatus currentStatus, DocumentApprovalPolicy policy) =>
        (currentStatus, policy) switch
        {
            (DocumentStatus.PendingApproval, DocumentApprovalPolicy.DirectPostingAllowed) => true,
            (DocumentStatus.PendingApproval, DocumentApprovalPolicy.TwoStepApprovalRequired) => true,
            (DocumentStatus.PendingApproval, DocumentApprovalPolicy.ThreeStepApprovalRequired) => true,
            (DocumentStatus.Reviewed, DocumentApprovalPolicy.ThreeStepApprovalRequired) => true,
            _ => false
        };

    /// <summary>
    /// رد سند؛ سند به پیش‌نویس بازمی‌گردد (دلیل رد در سند ثبت می‌شود).
    /// </summary>
    public static bool CanReject(DocumentStatus currentStatus) =>
        currentStatus is DocumentStatus.PendingApproval or DocumentStatus.Reviewed;

    /// <summary>
    /// ثبت قطعی؛ در سیاست‌های نیازمند تأیید فقط سند تأییدشده قطعی می‌شود.
    /// </summary>
    public static bool CanPost(DocumentStatus currentStatus, DocumentApprovalPolicy policy) =>
        (currentStatus, policy) switch
        {
            (DocumentStatus.Draft, DocumentApprovalPolicy.DirectPostingAllowed) => true,
            (DocumentStatus.Approved, _) => true,
            _ => false
        };

    /// <summary>
    /// معکوس‌سازی (Storno)؛ تنها اسناد قطعی‌شده قابل برگشت هستند.
    /// </summary>
    public static bool CanReverse(DocumentStatus currentStatus) => currentStatus == DocumentStatus.Posted;

    /// <summary>
    /// سند در وضعیت جاری قابل ویرایش سطرها/شرح و حذف است؟
    /// اسناد قطعی (Posted / Reversed) و اسناد در جریان تأیید، تغییرناپذیرند.
    /// </summary>
    public static bool CanEditOrDelete(DocumentStatus currentStatus) => currentStatus == DocumentStatus.Draft;

    public static void EnsureCanSubmit(DocumentStatus currentStatus)
    {
        if (!CanSubmit(currentStatus))
            throw new InvalidDocumentTransitionException(currentStatus, DocumentStatus.PendingApproval);
    }

    public static void EnsureCanApprove(DocumentStatus currentStatus, DocumentApprovalPolicy policy)
    {
        if (!CanApprove(currentStatus, policy))
            throw new InvalidDocumentTransitionException(currentStatus, DocumentStatus.Approved, policy);
    }

    public static void EnsureCanReject(DocumentStatus currentStatus)
    {
        if (!CanReject(currentStatus))
            throw new InvalidDocumentTransitionException(currentStatus, DocumentStatus.Draft);
    }

    public static void EnsureCanPost(DocumentStatus currentStatus, DocumentApprovalPolicy policy)
    {
        if (!CanPost(currentStatus, policy))
            throw new InvalidDocumentTransitionException(currentStatus, DocumentStatus.Posted, policy);
    }

    public static void EnsureCanReverse(DocumentStatus currentStatus)
    {
        if (!CanReverse(currentStatus))
            throw new InvalidDocumentTransitionException(currentStatus, DocumentStatus.Reversed);
    }

    public static void EnsureCanEditOrDelete(DocumentStatus currentStatus)
    {
        if (!CanEditOrDelete(currentStatus))
            throw new ImmutableDocumentException(Guid.Empty, currentStatus.ToString());
    }
}
