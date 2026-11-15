using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// اختصاص بعد مالی به ردیف سند حسابداری.
/// هر ردیف می‌تواند به چند بعد از انواع مختلف اختصاص یابد؛ از هر نوع بعد حداکثر
/// یک اختصاص به ازای هر ردیف مجاز است (پایه تحلیل مالی تک‌مقداری هر نوع در ردیف).
/// این جدول منبع حقیقت ابعاد ردیف است؛ ستون‌های قدیمی CostCenterId/ProjectId/
/// DetailAccountId هنگام ایجاد پیش‌نویس به این اختصاص‌ها نرمال‌سازی می‌شوند.
/// </summary>
public class DocumentLineDimension : Entity, ITenantScoped, ICompanyScoped
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentLineId { get; set; }

    public FinancialDimensionType DimensionType { get; set; }
    public Guid DimensionId { get; set; }

    public DocumentLineDimension()
    {
    }

    public DocumentLineDimension(
        Guid tenantId,
        Guid companyId,
        Guid documentId,
        Guid documentLineId,
        FinancialDimensionType dimensionType,
        Guid dimensionId)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext",
                "شناسه مستاجر و شرکت برای اختصاص بعد مالی الزامی است.");

        if (documentId == Guid.Empty || documentLineId == Guid.Empty || dimensionId == Guid.Empty)
            throw new AccountingDomainException("Accounting.DimensionReferenceRequired",
                "ارجاع سند، ردیف و بعد مالی برای اختصاص بعد مالی الزامی است.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        DocumentId = documentId;
        DocumentLineId = documentLineId;
        DimensionType = dimensionType;
        DimensionId = dimensionId;
    }
}

/// <summary>
/// اختصاص بعد مالی به سطر دفتر کل.
/// هنگام قطعی‌سازی سند، ابعاد هر ردیف به سطر دفتر کل همان ردیف منعکس می‌شود تا
/// گزارش‌های تحلیلی چندبعدی مستقیماً از منبع گردش قابل اجرا باشند.
/// </summary>
public class GeneralLedgerEntryDimension : Entity, ITenantScoped, ICompanyScoped
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GeneralLedgerEntryId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentLineId { get; set; }

    public FinancialDimensionType DimensionType { get; set; }
    public Guid DimensionId { get; set; }

    public GeneralLedgerEntryDimension()
    {
    }

    public GeneralLedgerEntryDimension(
        Guid tenantId,
        Guid companyId,
        Guid generalLedgerEntryId,
        Guid documentId,
        Guid documentLineId,
        FinancialDimensionType dimensionType,
        Guid dimensionId)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext",
                "شناسه مستاجر و شرکت برای اختصاص بعد مالی به دفتر کل الزامی است.");

        if (generalLedgerEntryId == Guid.Empty || documentId == Guid.Empty || dimensionId == Guid.Empty)
            throw new AccountingDomainException("Accounting.DimensionReferenceRequired",
                "ارجاع سطر دفتر کل و بعد مالی الزامی است.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        GeneralLedgerEntryId = generalLedgerEntryId;
        DocumentId = documentId;
        DocumentLineId = documentLineId;
        DimensionType = dimensionType;
        DimensionId = dimensionId;
    }
}
