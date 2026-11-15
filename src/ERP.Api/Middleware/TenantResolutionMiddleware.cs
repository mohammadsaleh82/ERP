using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Tenancy.Contracts.Interfaces;

namespace ERP.Api.Middleware;

public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    ILogger<TenantResolutionMiddleware> logger)
{
    public const string TenantHeaderKey = "X-Tenant-Id";
    public const string CompanyHeaderKey = "X-Company-Id";
    public const string BranchHeaderKey = "X-Branch-Id";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IServiceProvider serviceProvider)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Skip public/health/swagger endpoints
        if (path.StartsWith("/api/health") ||
            path.StartsWith("/health") ||
            path.StartsWith("/swagger") ||
            path.Contains("/identity/login") ||
            path.Contains("/identity/refresh-token"))
        {
            await next(context);
            return;
        }

        // 1. If user is authenticated, determine and validate membership
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdStr, out var userId))
            {
                Guid? requestedTenantId = null;
                Guid? requestedCompanyId = null;
                Guid? requestedBranchId = null;

                // Check headers
                if (context.Request.Headers.TryGetValue(TenantHeaderKey, out var tHeader) &&
                    Guid.TryParse(tHeader, out var parsedTenantId))
                {
                    requestedTenantId = parsedTenantId;
                }

                if (context.Request.Headers.TryGetValue(CompanyHeaderKey, out var cHeader) &&
                    Guid.TryParse(cHeader, out var parsedCompanyId))
                {
                    requestedCompanyId = parsedCompanyId;
                }

                if (context.Request.Headers.TryGetValue(BranchHeaderKey, out var bHeader) &&
                    Guid.TryParse(bHeader, out var parsedBranchId))
                {
                    requestedBranchId = parsedBranchId;
                }

                // Fallback to claims if not in header
                if (!requestedTenantId.HasValue)
                {
                    var claimT = context.User.FindFirst(SecurityConstants.TenantIdClaim)?.Value;
                    if (Guid.TryParse(claimT, out var ct)) requestedTenantId = ct;
                }

                if (!requestedCompanyId.HasValue)
                {
                    var claimC = context.User.FindFirst(SecurityConstants.CompanyIdClaim)?.Value;
                    if (Guid.TryParse(claimC, out var cc)) requestedCompanyId = cc;
                }

                if (!requestedBranchId.HasValue)
                {
                    var claimB = context.User.FindFirst(SecurityConstants.BranchIdClaim)?.Value;
                    if (Guid.TryParse(claimB, out var cb)) requestedBranchId = cb;
                }

                // Validate membership if Tenant and Company are requested
                if (requestedTenantId.HasValue && requestedCompanyId.HasValue)
                {
                    var validator = serviceProvider.GetService<ITenancyMembershipValidator>();
                    if (validator is not null)
                    {
                        var isMember = await validator.HasActiveMembershipAsync(
                            userId,
                            requestedTenantId.Value,
                            requestedCompanyId.Value,
                            context.RequestAborted);

                        // If user is not member of requested company and not global superadmin
                        var isSuperAdmin = context.User.IsInRole("Admin") ||
                                           context.User.HasClaim(SecurityConstants.PermissionsClaim, "system:admin");

                        if (!isMember && !isSuperAdmin)
                        {
                            logger.LogWarning(
                                "User {UserId} attempted unauthorized access to Tenant {TenantId}, Company {CompanyId}",
                                userId, requestedTenantId, requestedCompanyId);

                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            context.Response.ContentType = "application/problem+json";

                            var problem = new
                            {
                                type = "https://httpstatuses.com/403",
                                title = "Forbidden",
                                status = 403,
                                detail = "شما عضو فعال شرکت و سازمان درخواستی نیستید و دسترسی شما مسدود است.",
                                code = "Tenancy.ForbiddenMembership",
                                instance = context.Request.Path.Value
                            };

                            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
                            return;
                        }
                    }

                    tenantContext.SetContext(requestedTenantId.Value, requestedCompanyId.Value, requestedBranchId);
                }
            }
        }

        await next(context);
    }
}
