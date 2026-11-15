using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// سیاست استفاده از بعد مالی روی یک حساب.
/// هر حساب می‌تواند برای هر نوع بعد مالی دقیقاً یک سیاست داشته باشد:
///   Required  → ثبت روی حساب بدون این بعد مجاز نیست («حساب نیازمند تفصیلی باشد»)
///   Allowed   → فقط انواع لیست‌شده روی حساب پذیرفته می‌شوند («فقط نوع مشخصی قبول کند»)
///   Forbidden → این نوع بعد روی حساب مطلقاً ممنوع است
/// این سیاست‌ها مکمل پرچم‌های قدیمی RequireCostCenter/RequireProject/RequireDetailAccount
/// حساب هستند و هم‌زمان با آن‌ها اعمال می‌شوند.
/// </summary>
public class AccountDimensionPolicy : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }

    public FinancialDimensionType DimensionType { get; set; }
    public DimensionPolicyMode Mode { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public AccountDimensionPolicy()
    {
    }

    private AccountDimensionPolicy(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        FinancialDimensionType dimensionType,
        DimensionPolicyMode mode,
        string performedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        AccountId = accountId;
        DimensionType = dimensionType;
        Mode = mode;
        Version = 1;
        CreatedBy = performedBy;
    }

    /// <summary>تعریف یا تغییر سیاست یک نوع بعد روی حساب (Upsert).</summary>
    public static AccountDimensionPolicy CreateOrReplace(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        FinancialDimensionType dimensionType,
        DimensionPolicyMode mode,
        string performedBy)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || accountId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext",
                "شناسه مستاجر، شرکت و حساب برای تعریف سیاست بعد مالی الزامی است.");

        return new AccountDimensionPolicy(tenantId, companyId, accountId, dimensionType, mode, performedBy);
    }

    public void ChangeMode(DimensionPolicyMode mode, string performedBy)
    {
        if (Mode == mode)
            return;

        Mode = mode;
        Version++;
        LastModifiedBy = performedBy;
    }
}

/// <summary>
/// قاعده ترکیب ممنوع ابعاد مالی.
/// ترکیب دو بعد مالی روی یک ردیف سند را ممنوع می‌کند؛ قاعده می‌تواند در سطح «نوع»
/// (هر دو نوع با هم ممنوع‌اند) یا در سطح «مقدار» (این دو مقدار مشخص با هم ممنوع‌اند) باشد.
/// </summary>
public class DimensionCombinationRule : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public FinancialDimensionType FirstDimensionType { get; set; }
    public Guid? FirstDimensionId { get; set; }
    public FinancialDimensionType SecondDimensionType { get; set; }
    public Guid? SecondDimensionId { get; set; }

    public string? Description { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public DimensionCombinationRule()
    {
    }

    private DimensionCombinationRule(
        Guid tenantId,
        Guid companyId,
        FinancialDimensionType firstType,
        Guid? firstDimensionId,
        FinancialDimensionType secondType,
        Guid? secondDimensionId,
        string? description,
        string performedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        FirstDimensionType = firstType;
        FirstDimensionId = firstDimensionId;
        SecondDimensionType = secondType;
        SecondDimensionId = secondDimensionId;
        Description = description?.Trim();
        Version = 1;
        CreatedBy = performedBy;
    }

    public static DimensionCombinationRule CreateForbidden(
        Guid tenantId,
        Guid companyId,
        FinancialDimensionType firstType,
        Guid? firstDimensionId,
        FinancialDimensionType secondType,
        Guid? secondDimensionId,
        string? description,
        string performedBy)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext",
                "شناسه مستاجر و شرکت برای تعریف قاعده ترکیب ابعاد الزامی است.");

        if (firstType == secondType && firstDimensionId == secondDimensionId)
            throw new AccountingDomainException("Accounting.DimensionCombinationSelfReferential",
                "قاعده ترکیب ممنوع نمی‌تواند یک بعد را با خودش مقایسه کند.");

        if (firstDimensionId.HasValue != secondDimensionId.HasValue)
            throw new AccountingDomainException("Accounting.DimensionCombinationLevelMismatch",
                "هر دو طرف قاعده ترکیب باید یا در سطح نوع باشند یا در سطح مقدار مشخص.");

        return new DimensionCombinationRule(
            tenantId, companyId, firstType, firstDimensionId, secondType, secondDimensionId, description, performedBy);
    }

    /// <summary>آیا این قاعده با جفت ابعاد ردیف (نوع/شناسه) مطابقت دارد؟</summary>
    public bool Matches(FinancialDimensionType typeA, Guid idA, FinancialDimensionType typeB, Guid idB)
    {
        return MatchesOrdered(FirstDimensionType, FirstDimensionId, SecondDimensionType, SecondDimensionId, typeA, idA, typeB, idB)
               || MatchesOrdered(FirstDimensionType, FirstDimensionId, SecondDimensionType, SecondDimensionId, typeB, idB, typeA, idA);
    }

    private static bool MatchesOrdered(
        FinancialDimensionType ruleFirstType, Guid? ruleFirstId,
        FinancialDimensionType ruleSecondType, Guid? ruleSecondId,
        FinancialDimensionType typeA, Guid idA,
        FinancialDimensionType typeB, Guid idB)
    {
        var firstMatches = ruleFirstType == typeA && (!ruleFirstId.HasValue || ruleFirstId.Value == idA);
        var secondMatches = ruleSecondType == typeB && (!ruleSecondId.HasValue || ruleSecondId.Value == idB);
        return firstMatches && secondMatches;
    }
}
