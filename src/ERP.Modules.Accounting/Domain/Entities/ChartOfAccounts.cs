using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

public class AccountGroup : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountNature Nature { get; set; } = AccountNature.Asset;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];

    public AccountGroup()
    {
    }

    public AccountGroup(Guid tenantId, Guid companyId, string code, string name, AccountNature nature)
    {
        if (tenantId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");
        if (string.IsNullOrWhiteSpace(code)) throw new AccountingDomainException("Accounting.EmptyCode", "کد گروه حساب الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new AccountingDomainException("Accounting.EmptyName", "نام گروه حساب الزامی است.");

        TenantId = tenantId;
        CompanyId = companyId;
        Code = code.Trim();
        Name = name.Trim();
        Nature = nature;
    }
}

public class Account : AggregateRoot, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid? ParentAccountId { get; set; }
    public Guid? AccountGroupId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string FullCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public AccountLevel Level { get; set; } = AccountLevel.General;
    public AccountNature Nature { get; set; } = AccountNature.Asset;
    public NormalBalance NormalBalance { get; set; } = NormalBalance.Debit;

    public bool IsActive { get; set; } = true;
    public bool IsSystemAccount { get; set; }
    public bool AllowPosting { get; set; }

    public bool RequireCostCenter { get; set; }
    public bool RequireProject { get; set; }
    public bool RequireDetailAccount { get; set; }

    public uint Version { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public AccountGroup? AccountGroup { get; set; }
    public Account? ParentAccount { get; set; }
    public ICollection<Account> SubAccounts { get; set; } = [];

    /// <summary>سیاست‌های ابعاد مالی این حساب (اجباری/مجاز/ممنوع به ازای هر نوع بعد).</summary>
    public ICollection<AccountDimensionPolicy> DimensionPolicies { get; set; } = [];

    public Account()
    {
    }

    /// <summary>
    /// سازنده ایجاد حساب ریشه (سطح گروه حساب)
    /// </summary>
    public static Account CreateRoot(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        AccountNature nature,
        NormalBalance? normalBalance = null,
        string? description = null,
        bool isSystemAccount = false)
    {
        if (tenantId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");
        if (string.IsNullOrWhiteSpace(code)) throw new AccountingDomainException("Accounting.EmptyCode", "کد حساب الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new AccountingDomainException("Accounting.EmptyName", "نام حساب الزامی است.");

        return new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ParentAccountId = null,
            Code = code.Trim(),
            FullCode = code.Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Level = AccountLevel.Group,
            Nature = nature,
            NormalBalance = normalBalance ?? nature.GetDefaultNormalBalance(),
            IsActive = true,
            IsSystemAccount = isSystemAccount,
            AllowPosting = false // حساب گروه هرگز قابل ثبت سند نیست
        };
    }

    /// <summary>
    /// سازنده ایجاد حساب فرزند با بررسی دقیق سازگاری والد و فرزند
    /// </summary>
    public Account CreateChild(
        string code,
        string name,
        string? description = null,
        bool isSystemAccount = false,
        NormalBalance? normalBalance = null,
        bool allowPosting = true)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new AccountingDomainException("Accounting.EmptyCode", "کد حساب الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new AccountingDomainException("Accounting.EmptyName", "نام حساب الزامی است.");

        var targetLevel = (AccountLevel)((int)Level + 1);
        if ((int)targetLevel > (int)AccountLevel.Detail)
        {
            throw new AccountingDomainException("Accounting.MaxLevelExceeded",
                $"امکان ایجاد زیرحساب برای حساب سطح تفصیلی '{FullCode}' وجود ندارد (سقف مجاز ۴ سطح).");
        }

        // حساب والد نباید سند پذیرفته باشد؛ اگر والد فرزند داشته باشد نباید AllowPosting باشد
        AllowPosting = false;

        var childFullCode = $"{FullCode}{code.Trim()}";

        var child = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CompanyId = CompanyId,
            ParentAccountId = Id,
            ParentAccount = this,
            AccountGroupId = AccountGroupId,
            Code = code.Trim(),
            FullCode = childFullCode,
            Name = name.Trim(),
            Description = description?.Trim(),
            Level = targetLevel,
            Nature = Nature, // ارث‌بری ماهیت کلان از والد
            NormalBalance = normalBalance ?? Nature.GetDefaultNormalBalance(),
            IsActive = true,
            IsSystemAccount = isSystemAccount,
            // سطوح گروه و کل تجمیعی هستند و ثبت سند روی آن‌ها مجاز نیست؛ فقط معین و تفصیلی مجازند
            AllowPosting = targetLevel is AccountLevel.Subsidiary or AccountLevel.Detail ? allowPosting : false
        };

        SubAccounts.Add(child);
        return child;
    }

    public void Update(
        string name,
        string? description,
        NormalBalance normalBalance,
        bool requireCostCenter,
        bool requireProject,
        bool requireDetailAccount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new AccountingDomainException("Accounting.EmptyName", "نام حساب نمی‌تواند خالی باشد.");

        Name = name.Trim();
        Description = description?.Trim();
        NormalBalance = normalBalance;
        RequireCostCenter = requireCostCenter;
        RequireProject = requireProject;
        RequireDetailAccount = requireDetailAccount;
    }

    public void ChangeParent(Account newParent)
    {
        if (newParent == null)
            throw new AccountingDomainException("Accounting.NullParent", "حساب والد نمی‌تواند خالی باشد.");

        if (newParent.CompanyId != CompanyId || newParent.TenantId != TenantId)
            throw new AccountingDomainException("Accounting.CrossCompanyParentForbidden",
                "والد و فرزند باید به یک شرکت و مستاجر یکسان تعلق داشته باشند.");

        if (newParent.Id != Guid.Empty && newParent.Id == Id)
            throw new AccountingDomainException("Accounting.SelfParentingForbidden", "یک حساب نمی‌تواند والد خودش باشد (ایجاد چرخه ممنوع است).");

        // بررسی عدم ایجاد چرخه در درخت (Cycle Detection): والد جدید نباید در بین نوادگان این حساب باشد
        if (IsDescendantOf(newParent))
        {
            throw new AccountingDomainException("Accounting.HierarchyCycleForbidden",
                $"ایجاد چرخه در ساختار درختی حساب‌ها ممنوع است. حساب '{newParent.Name}' فرزند یا نواده این حساب است.");
        }

        // بررسی سازگاری سطح: سطح فرزند باید دقیقاً یک سطح بعد از والد باشد
        if ((int)newParent.Level + 1 != (int)Level)
        {
            throw new AccountingDomainException("Accounting.IncompatibleLevelHierarchy",
                $"سطح حساب والد ({newParent.Level}) با سطح این حساب ({Level}) سازگار نیست. سطح فرزند باید بلافاصله پس از والد قرار گیرد.");
        }

        ParentAccountId = newParent.Id;
        ParentAccount = newParent;
        Nature = newParent.Nature;
        FullCode = $"{newParent.FullCode}{Code}";
        newParent.AllowPosting = false; // والد اکنون دارای فرزند است و نمی‌تواند سند مستقیم بپذیرد
    }

    public void ChangeLevel(AccountLevel newLevel)
    {
        if (SubAccounts.Count > 0 && newLevel > Level)
        {
            throw new AccountingDomainException("Accounting.CannotDowngradeAccountWithChildren",
                "امکان تغییر سطح حسابی که دارای زیرمجموعه است به سطوح پایین‌تر وجود ندارد.");
        }

        Level = newLevel;
        if (newLevel is AccountLevel.Group or AccountLevel.General)
        {
            AllowPosting = false;
        }
    }

    public void Deactivate()
    {
        if (SubAccounts.Any(c => c.IsActive))
        {
            throw new AccountingDomainException("Accounting.CannotDeactivateParentWithActiveChildren",
                "امکان غیرفعال کردن حسابی که زیرحساب‌های فعال دارد وجود ندارد. لطفاً ابتدا زیرحساب‌ها را غیرفعال فرمایید.");
        }

        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void EnsureCanPost()
    {
        if (!IsActive)
            throw new AccountingDomainException("Accounting.AccountInactive",
                $"حساب '{Name}' ({FullCode}) غیرفعال است و امکان ثبت سند جدید روی آن مسدود می‌باشد.");

        if (!AllowPosting || SubAccounts.Count > 0 || Level is AccountLevel.Group or AccountLevel.General)
            throw new AccountingDomainException("Accounting.AccountDirectPostingForbidden",
                $"حساب '{Name}' ({FullCode}) یک حساب تجمیعی و کنترلی است. ثبت مستقیم سند روی حساب‌های تجمیعی و دارای زیرمجموعه ممنوع است.");
    }

    public decimal CalculateNormalBalance(decimal debitTotal, decimal creditTotal)
    {
        return NormalBalance == NormalBalance.Debit
            ? (debitTotal - creditTotal)
            : (creditTotal - debitTotal);
    }

    private bool IsDescendantOf(Account target)
    {
        var current = target;
        while (current != null)
        {
            if (current.ParentAccountId == Id || current.ParentAccount?.Id == Id)
                return true;
            current = current.ParentAccount;
        }
        return false;
    }
}
