namespace ERP.BuildingBlocks.Security;

public static class SecurityConstants
{
    public const string TenantIdClaim = "tenant_id";
    public const string CompanyIdClaim = "company_id";
    public const string BranchIdClaim = "branch_id";
    public const string PermissionsClaim = "permissions";
    public const string SessionIdClaim = "session_id";
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    Guid? ActiveTenantId { get; }
    Guid? ActiveCompanyId { get; }
    IReadOnlyList<string> Permissions { get; }
    bool HasPermission(string permission);
}
