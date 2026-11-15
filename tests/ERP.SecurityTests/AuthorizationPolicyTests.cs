using System.Security.Claims;
using ERP.BuildingBlocks.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.SecurityTests;

public class AuthorizationPolicyTests
{
    private sealed class MockUserContext : ICurrentUserContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Email { get; set; } = "test@erp.local";
        public bool IsAuthenticated { get; set; } = true;
        public Guid? ActiveTenantId { get; set; } = Guid.NewGuid();
        public Guid? ActiveCompanyId { get; set; } = Guid.NewGuid();
        public Guid? ActiveBranchId { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyList<string> Permissions { get; set; } = [];

        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    [Fact]
    public async Task PermissionAuthorizationHandler_WhenUserHasPermission_ShouldSucceed()
    {
        var requiredPermission = "accounting:vouchers:post";
        var mockUser = new MockUserContext
        {
            Permissions = ["accounting:vouchers:read", requiredPermission]
        };

        var handler = new PermissionAuthorizationHandler(mockUser);
        var requirement = new PermissionRequirement(requiredPermission);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, mockUser.UserId.ToString()!)], "Bearer"));

        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PermissionAuthorizationHandler_WhenUserLacksPermission_ShouldFail()
    {
        var requiredPermission = "treasury:bank-accounts:delete";
        var mockUser = new MockUserContext
        {
            Permissions = ["treasury:bank-accounts:read"]
        };

        var handler = new PermissionAuthorizationHandler(mockUser);
        var requirement = new PermissionRequirement(requiredPermission);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, mockUser.UserId.ToString()!)], "Bearer"));

        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PermissionPolicyProvider_ShouldDynamicallyGeneratePolicyWithPermissionRequirement()
    {
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionPolicyProvider(options);

        var policyName = "inventory:products:create";
        var policy = await provider.GetPolicyAsync(policyName);

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<PermissionRequirement>().Should().ContainSingle(r => r.Permission == policyName);
    }
}
