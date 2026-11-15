namespace ERP.BuildingBlocks.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool IsResolved => TenantId.HasValue && TenantId.Value != Guid.Empty;

    public void SetContext(Guid tenantId, Guid? companyId = null, Guid? branchId = null)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        BranchId = branchId;
    }
}
