using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// بعد مالی (Financial Dimension).
/// یک مفهوم شناور (تفصیلی، مرکز هزینه، پروژه، شعبه، قرارداد، شخص، مشتری،
/// تأمین‌کننده، کارمند یا بعد سفارشی) که ردیف‌های اسناد و سطرهای دفتر کل به آن
/// اختصاص می‌یابند تا تحلیل مالی چندبعدی ممکن شود.
/// ابعادی که مالکیت آن‌ها در ماژول‌های دیگر است (Party/Customer/Supplier/Employee)
/// با شناسه مرجع خارجی (<see cref="ExternalReferenceId"/>) و نام ماژول مالک
/// (<see cref="OwnerModule"/>) به‌صورت آینه مرجع در اینجا ثبت می‌شوند؛ هیچ داده
/// اضافه‌ای از ماژول مالک تکرار نمی‌شود و همگام‌سازی از طریق قرارداد ماژول انجام می‌گیرد.
/// </summary>
public class FinancialDimension : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public FinancialDimensionType DimensionType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>شناسه موجودیت در ماژول مالک (مثلاً PartyId در ماژول Parties). برای ابعاد داخلی null.</summary>
    public Guid? ExternalReferenceId { get; set; }

    /// <summary>نام ماژول مالک ابعاد خارجی (مثلاً "Parties") یا null برای ابعاد داخلی حسابداری.</summary>
    public string? OwnerModule { get; set; }

    public bool IsActive { get; set; } = true;

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public FinancialDimension()
    {
    }

    public static FinancialDimension Create(
        Guid tenantId,
        Guid companyId,
        FinancialDimensionType dimensionType,
        string code,
        string name,
        string performedBy,
        Guid? externalReferenceId = null,
        string? ownerModule = null)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext", "شناسه مستاجر و شرکت برای ایجاد بعد مالی الزامی است.");

        code = code?.Trim() ?? string.Empty;
        name = name?.Trim() ?? string.Empty;

        if (code.Length == 0)
            throw new AccountingDomainException("Accounting.DimensionCodeRequired", "کد بعد مالی الزامی است.");

        if (code.Length > 30)
            throw new AccountingDomainException("Accounting.DimensionCodeTooLong", "کد بعد مالی حداکثر می‌تواند ۳۰ کاراکتر باشد.");

        if (name.Length == 0)
            throw new AccountingDomainException("Accounting.DimensionNameRequired", "نام بعد مالی الزامی است.");

        if (name.Length > 200)
            throw new AccountingDomainException("Accounting.DimensionNameTooLong", "نام بعد مالی حداکثر می‌تواند ۲۰۰ کاراکتر باشد.");

        if (externalReferenceId.HasValue && string.IsNullOrWhiteSpace(ownerModule))
            throw new AccountingDomainException("Accounting.DimensionOwnerModuleRequired",
                "بعد مالی با مرجع خارجی باید ماژول مالک را مشخص کند.");

        return new FinancialDimension
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            DimensionType = dimensionType,
            Code = code,
            Name = name,
            ExternalReferenceId = externalReferenceId,
            OwnerModule = ownerModule,
            IsActive = true,
            Version = 1,
            CreatedBy = performedBy
        };
    }

    public void UpdateName(string name, string performedBy)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new AccountingDomainException("Accounting.DimensionNameRequired", "نام بعد مالی الزامی است.");

        Name = name;
        Version++;
        LastModifiedBy = performedBy;
    }

    /// <summary>همگام‌سازی آینه مرجع با ماژول مالک (به‌روزرسانی کد/نام).</summary>
    public void SyncFromOwnerModule(string code, string name)
    {
        code = code?.Trim() ?? string.Empty;
        name = name?.Trim() ?? string.Empty;

        if (code.Length > 0)
            Code = code;

        if (name.Length > 0)
            Name = name;

        Version++;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        Version++;
    }

    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        Version++;
    }
}
