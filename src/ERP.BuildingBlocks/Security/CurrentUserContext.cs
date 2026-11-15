using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ERP.BuildingBlocks.Security;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    Guid? ActiveTenantId { get; }
    Guid? ActiveCompanyId { get; }
    Guid? ActiveBranchId { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool HasPermission(string permission);
    bool IsInRole(string role);
}

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? ActiveTenantId
    {
        get
        {
            var claim = User?.FindFirst(SecurityConstants.TenantIdClaim)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid? ActiveCompanyId
    {
        get
        {
            var claim = User?.FindFirst(SecurityConstants.CompanyIdClaim)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid? ActiveBranchId
    {
        get
        {
            var claim = User?.FindFirst(SecurityConstants.BranchIdClaim)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Distinct()
        .ToList() ?? [];

    public IReadOnlyList<string> Permissions => User?.FindAll(SecurityConstants.PermissionsClaim)
        .Select(c => c.Value)
        .Distinct()
        .ToList() ?? [];

    public bool HasPermission(string permission)
    {
        if (!IsAuthenticated) return false;
        if (IsInRole("Admin") || Permissions.Contains("system:admin")) return true;
        return Permissions.Contains(permission);
    }

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
