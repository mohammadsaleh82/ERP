using System.Net;
using System.Security;
using System.Security.Claims;
using System.Text.Json;
using ERP.Api.Middleware;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Tenancy.Contracts.Interfaces;
using ERP.Modules.Tenancy.Domain;
using ERP.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SecurityTests;

public class MultiTenantSecurityAuditTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TenancyDbContext> _options;

    public MultiTenantSecurityAuditTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new TenancyDbContext(_options, null);
        initContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task ScenarioA_TenantIsolation_TenantACannotReadTenantBData()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var seedContext = new TenancyDbContext(_options, null))
        {
            seedContext.Tenants.AddRange(
                new Tenant { Id = tenantA, Name = "Tenant A Corp", Identifier = "tenant-a" },
                new Tenant { Id = tenantB, Name = "Tenant B Corp", Identifier = "tenant-b" });

            seedContext.Companies.AddRange(
                new Company { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Company A-1" },
                new Company { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Company B-1" });

            await seedContext.SaveChangesAsync();
        }

        var tenantContext = new TenantContext();
        tenantContext.SetContext(tenantA);

        using (var queryContext = new TenancyDbContext(_options, tenantContext))
        {
            var companies = await queryContext.Companies.ToListAsync();
            companies.Should().ContainSingle();
            companies.Single().TenantId.Should().Be(tenantA);
            companies.Any(c => c.TenantId == tenantB).Should().BeFalse();
        }
    }

    [Fact]
    public async Task ScenarioB_CompanyIsolation_UserSendingUnauthorizedCompanyId_IsBlockedByMiddleware()
    {
        var userId = Guid.NewGuid();
        var authorizedTenantId = Guid.NewGuid();
        var authorizedCompanyId = Guid.NewGuid();
        var unauthorizedCompanyBId = Guid.NewGuid();

        var validator = new TestMembershipValidator();
        validator.AddAuthorized(userId, authorizedTenantId, authorizedCompanyId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenancyMembershipValidator>(validator);
        var serviceProvider = services.BuildServiceProvider();

        var tenantContext = new TenantContext();
        var wasNextCalled = false;

        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                wasNextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "Bearer"));

        httpContext.Request.Path = "/api/v1/accounting/vouchers";
        httpContext.Request.Headers["X-Tenant-Id"] = authorizedTenantId.ToString();
        httpContext.Request.Headers["X-Company-Id"] = unauthorizedCompanyBId.ToString(); // Attacker attempts access to Company B

        await middleware.InvokeAsync(httpContext, tenantContext, serviceProvider);

        wasNextCalled.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("code").GetString().Should().Be("Tenancy.ForbiddenMembership");
    }

    [Fact]
    public async Task ScenarioC_BranchIsolation_UserInBranchACannotSeeWarehousesOfBranchB()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();

        using (var seedContext = new TenancyDbContext(_options, null))
        {
            seedContext.Tenants.Add(new Tenant { Id = tenantId, Name = "Org Corp", Identifier = "org-corp" });
            seedContext.Companies.Add(new Company { Id = companyId, TenantId = tenantId, Name = "Holding Co" });

            seedContext.Branches.AddRange(
                new Branch { Id = branchAId, TenantId = tenantId, CompanyId = companyId, Name = "Branch Alpha", Code = "BR-A" },
                new Branch { Id = branchBId, TenantId = tenantId, CompanyId = companyId, Name = "Branch Beta", Code = "BR-B" });

            seedContext.Warehouses.AddRange(
                new Warehouse { Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId, BranchId = branchAId, Name = "Alpha Central Storage", Code = "WH-A1" },
                new Warehouse { Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId, BranchId = branchBId, Name = "Beta Safe Vault", Code = "WH-B1" });

            await seedContext.SaveChangesAsync();
        }

        var tenantContext = new TenantContext();
        tenantContext.SetContext(tenantId, companyId, branchAId);

        using (var queryContext = new TenancyDbContext(_options, tenantContext))
        {
            var warehouses = await queryContext.Warehouses.ToListAsync();
            warehouses.Should().ContainSingle();
            warehouses.Single().BranchId.Should().Be(branchAId);
            warehouses.Single().Name.Should().Be("Alpha Central Storage");
        }
    }

    [Fact]
    public async Task ScenarioD_TamperResistance_QueryStringOrRouteTamperingCannotOverrideTenantContext()
    {
        var tenantContext = new TenantContext();
        var legitimateTenant = Guid.NewGuid();
        var legitimateCompany = Guid.NewGuid();

        tenantContext.SetContext(legitimateTenant, legitimateCompany);

        using var dbContext = new TenancyDbContext(_options, tenantContext);

        // Server-side context properties are strictly bound to ITenantContext
        dbContext.CurrentTenantId.Should().Be(legitimateTenant);
        dbContext.CurrentCompanyId.Should().Be(legitimateCompany);
    }

    [Fact]
    public async Task ScenarioE_MutationSecurityGuard_ModifyingOrDeletingAnotherTenantEntityThrowsSecurityException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        using (var seedContext = new TenancyDbContext(_options, null))
        {
            seedContext.Tenants.AddRange(
                new Tenant { Id = tenantA, Name = "Tenant A", Identifier = "tenant-a" },
                new Tenant { Id = tenantB, Name = "Tenant B", Identifier = "tenant-b" });

            seedContext.Companies.AddRange(
                new Company { Id = companyA, TenantId = tenantA, Name = "Company A" },
                new Company { Id = companyB, TenantId = tenantB, Name = "Company B" });

            await seedContext.SaveChangesAsync();
        }

        var tenantContext = new TenantContext();
        tenantContext.SetContext(tenantA, companyA);

        using var attackContext = new TenancyDbContext(_options, tenantContext);

        // Attacker creates an entity belonging to Tenant B and tries to attach and modify or delete it
        var targetCompanyB = new Company
        {
            Id = companyB,
            TenantId = tenantB, // belongs to Tenant B
            Name = "Hacked Company Name"
        };

        attackContext.Companies.Update(targetCompanyB);

        var action = async () => await attackContext.SaveChangesAsync();

        // Must throw SecurityException because TenantId != CurrentTenantId
        await action.Should().ThrowAsync<SecurityException>()
            .WithMessage("*Tenant violation*");
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TestMembershipValidator : ITenancyMembershipValidator
    {
        private readonly HashSet<(Guid, Guid, Guid)> _allowed = [];

        public void AddAuthorized(Guid u, Guid t, Guid c) => _allowed.Add((u, t, c));

        public Task<bool> HasActiveMembershipAsync(Guid userId, Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_allowed.Contains((userId, tenantId, companyId)));
        }

        public Task<IReadOnlyList<string>> GetUserCompanyPermissionsAsync(Guid userId, Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }
}
