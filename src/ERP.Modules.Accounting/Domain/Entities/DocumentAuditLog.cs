using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// لاگ ممیزی چرخه عمر سند حسابداری (Audit Trail).
/// برای هر عملیات حیاتی (ایجاد، ویرایش، ارسال، تأیید، رد، قطعی‌سازی، معکوس‌سازی)
/// یک رکورد ممیزی با کاربر واقعی انجام‌دهنده ثبت می‌شود.
/// رکوردهای ممیزی حتی پس از حذف سند پیش‌نویس حفظ می‌شوند (بدون وابستگی FK به سند).
/// </summary>
public class DocumentAuditLog : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid DocumentId { get; set; }

    public string Action { get; set; } = string.Empty;
    public DocumentStatus? PreviousStatus { get; set; }
    public DocumentStatus? NewStatus { get; set; }
    public string? Details { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTimeOffset PerformedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public DocumentAuditLog()
    {
    }

    public static DocumentAuditLog Create(
        Guid tenantId,
        Guid companyId,
        Guid documentId,
        string action,
        DocumentStatus? previousStatus,
        DocumentStatus? newStatus,
        string performedBy,
        string? details = null,
        DateTimeOffset? performedAtUtc = null)
    {
        if (tenantId == Guid.Empty) throw new Exceptions.AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new Exceptions.AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");
        if (documentId == Guid.Empty) throw new Exceptions.AccountingDomainException("Accounting.EmptyDocumentId", "شناسه سند برای ثبت ممیزی الزامی است.");
        if (string.IsNullOrWhiteSpace(action)) throw new Exceptions.AccountingDomainException("Accounting.EmptyAuditAction", "نوع عملیات ممیزی الزامی است.");
        if (string.IsNullOrWhiteSpace(performedBy)) throw new Exceptions.AccountingDomainException("Accounting.EmptyActor", "کاربر انجام‌دهنده عملیات الزامی است.");

        return new DocumentAuditLog
        {
            TenantId = tenantId,
            CompanyId = companyId,
            DocumentId = documentId,
            Action = action.Trim(),
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Details = details?.Trim(),
            PerformedBy = performedBy.Trim(),
            PerformedAtUtc = performedAtUtc ?? DateTimeOffset.UtcNow
        };
    }
}

/// <summary>مقادیر استاندارد عملیات ممیزی چرخه عمر سند.</summary>
public static class DocumentAuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Submitted = "Submitted";
    public const string Reviewed = "Reviewed";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Posted = "Posted";
    public const string Reversed = "Reversed";
}
