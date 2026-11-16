namespace ERP.BuildingBlocks.Tenancy;

public interface ITenantScoped
{
    Guid TenantId { get; }
}

public interface ICompanyScoped : ITenantScoped
{
    Guid CompanyId { get; }
}

public interface IBranchScoped : ICompanyScoped
{
    Guid BranchId { get; }
}

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? CompanyId { get; }
    Guid? BranchId { get; }
    bool IsResolved { get; }
    void SetContext(Guid tenantId, Guid? companyId = null, Guid? branchId = null);
}
