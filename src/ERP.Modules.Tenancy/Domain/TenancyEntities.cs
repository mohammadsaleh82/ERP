using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;

namespace ERP.Modules.Tenancy.Domain;

public class Tenant : Entity, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public ICollection<Company> Companies { get; set; } = [];
}

public class Company : Entity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public int ActiveFiscalYear { get; set; } = 1403;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<Branch> Branches { get; set; } = [];
}

public class Branch : Entity, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public Company? Company { get; set; }
    public ICollection<Warehouse> Warehouses { get; set; } = [];
}

public class Warehouse : Entity, IBranchScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public Branch? Branch { get; set; }
}

public class UserCompanyMembership : Entity, ITenantScoped
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public string Permissions { get; set; } = string.Empty; // Comma-separated permissions list
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
