using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ERP.Api.Middleware;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Tenancy.Contracts.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SecurityTests;

public class CompanyMembershipMiddlewareTests
{
    private sealed class MockMembershipValidator : ITenancyMembershipValidator
    {
        private readonly HashSet<(Guid UserId, Guid TenantId, Guid CompanyId)> _activeMemberships = [];

        public void AddMembership(Guid userId, Guid tenantId, Guid companyId)
        {
            _activeMemberships.Add((userId, tenantId, companyId));
        }

        public Task<bool> HasActiveMembershipAsync(Guid userId, Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
        {
            var isMember = _activeMemberships.Contains((userId, tenantId, companyId));
            return Task.FromResult(isMember);
        }

        public Task<IReadOnlyList<string>> GetUserCompanyPermissionsAsync(Guid userId, Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    [Fact]
    public async Task WhenUserHasActiveMembership_ShouldAllowRequestAndSetTenantContext()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var validator = new MockMembershipValidator();
        validator.AddMembership(userId, tenantId, companyId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenancyMembershipValidator>(validator);
        var serviceProvider = services.BuildServiceProvider();

        var tenantContext = new TenantContext();
        var nextInvoked = false;

        var middleware = new TenantResolutionMiddleware(
            next: (innerContext) =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance
        );

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "Bearer"));

        httpContext.Request.Path = "/api/v1/test-resource";
        httpContext.Request.Headers[TenantResolutionMiddleware.TenantHeaderKey] = tenantId.ToString();
        httpContext.Request.Headers[TenantResolutionMiddleware.CompanyHeaderKey] = companyId.ToString();

        await middleware.InvokeAsync(httpContext, tenantContext, serviceProvider);

        nextInvoked.Should().BeTrue();
        tenantContext.TenantId.Should().Be(tenantId);
        tenantContext.CompanyId.Should().Be(companyId);
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenUserAttemptsAccessToUnauthorizedCompany_ShouldReturn403ForbiddenWithProblemDetails()
    {
        var userId = Guid.NewGuid();
        var authorizedTenantId = Guid.NewGuid();
        var authorizedCompanyId = Guid.NewGuid();

        var attackerTargetCompanyId = Guid.NewGuid(); // Company that user does NOT belong to

        var validator = new MockMembershipValidator();
        validator.AddMembership(userId, authorizedTenantId, authorizedCompanyId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenancyMembershipValidator>(validator);
        var serviceProvider = services.BuildServiceProvider();

        var tenantContext = new TenantContext();
        var nextInvoked = false;

        var middleware = new TenantResolutionMiddleware(
            next: (innerContext) =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "Bearer"));

        httpContext.Request.Path = "/api/v1/confidential-records";
        httpContext.Request.Headers[TenantResolutionMiddleware.TenantHeaderKey] = authorizedTenantId.ToString();
        httpContext.Request.Headers[TenantResolutionMiddleware.CompanyHeaderKey] = attackerTargetCompanyId.ToString(); // Forged or unauthorized header!

        await middleware.InvokeAsync(httpContext, tenantContext, serviceProvider);

        // Crucial security invariant: Next middleware MUST NOT be invoked!
        nextInvoked.Should().BeFalse();

        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        httpContext.Response.ContentType.Should().Contain("application/problem+json");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(403);
        root.GetProperty("code").GetString().Should().Be("Tenancy.ForbiddenMembership");
        root.GetProperty("title").GetString().Should().Be("Forbidden");
    }
}
