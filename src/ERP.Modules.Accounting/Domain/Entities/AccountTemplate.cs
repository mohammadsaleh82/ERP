using ERP.BuildingBlocks.Persistence;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;

namespace ERP.Modules.Accounting.Domain.Entities;

public class AccountTemplate : Entity, IAuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSystemTemplate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public ICollection<AccountTemplateLevel> Levels { get; set; } = [];
    public ICollection<AccountTemplateItem> Items { get; set; } = [];

    public AccountTemplate()
    {
    }

    public AccountTemplate(string title, string? description = null, bool isDefault = false, bool isSystemTemplate = false)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new AccountingDomainException("Accounting.EmptyTemplateTitle", "عنوان الگوی کدینگ الزامی است.");

        Title = title.Trim();
        Description = description?.Trim();
        IsDefault = isDefault;
        IsSystemTemplate = isSystemTemplate;
    }

    public void AddLevel(AccountLevel level, string name, int codeLength)
    {
        if (codeLength <= 0 || codeLength > 10)
            throw new AccountingDomainException("Accounting.InvalidCodeLength", "طول کد هر سطح باید بین ۱ تا ۱۰ رقم باشد.");

        Levels.Add(new AccountTemplateLevel
        {
            Id = Guid.NewGuid(),
            TemplateId = Id,
            Level = level,
            Name = name.Trim(),
            CodeLength = codeLength
        });
    }

    public void AddItem(
        string code,
        string name,
        AccountLevel level,
        AccountNature nature,
        NormalBalance normalBalance,
        string? parentCode = null,
        bool allowPosting = false,
        string? description = null,
        int sortOrder = 0)
    {
        Items.Add(new AccountTemplateItem
        {
            Id = Guid.NewGuid(),
            TemplateId = Id,
            Code = code.Trim(),
            Name = name.Trim(),
            ParentCode = string.IsNullOrWhiteSpace(parentCode) ? null : parentCode.Trim(),
            Level = level,
            Nature = nature,
            NormalBalance = normalBalance,
            AllowPosting = allowPosting,
            Description = description?.Trim(),
            SortOrder = sortOrder
        });
    }
}

public class AccountTemplateLevel : Entity
{
    public Guid TemplateId { get; set; }
    public AccountLevel Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CodeLength { get; set; }

    public AccountTemplate? Template { get; set; }
}

public class AccountTemplateItem : Entity
{
    public Guid TemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentCode { get; set; }

    public AccountLevel Level { get; set; }
    public AccountNature Nature { get; set; }
    public NormalBalance NormalBalance { get; set; }
    public bool AllowPosting { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public AccountTemplate? Template { get; set; }
}
