using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Tenancy.Domain;
using ERP.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

public class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly DbContextOptions<TenancyDbContext> _options;

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = new TenantContext();

        _options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create database schema
        using var initContext = new TenancyDbContext(_options, null);
        initContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task TenantIsolation_UserFromTenantOne_CannotReadDataFromTenantTwo()
    {
        // 1. Arrange: Seed 2 independent Tenants with companies and branches
        var tenant1Id = Guid.NewGuid();
        var company1Id = Guid.NewGuid();
        var branch1Id = Guid.NewGuid();

        var tenant2Id = Guid.NewGuid();
        var company2Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();

        using (var seedContext = new TenancyDbContext(_options, null))
        {
            var tenant1 = new Tenant { Id = tenant1Id, Name = "Tenant One Corp", Identifier = "tenant-1" };
            var tenant2 = new Tenant { Id = tenant2Id, Name = "Tenant Two LLC", Identifier = "tenant-2" };

            var company1 = new Company { Id = company1Id, TenantId = tenant1Id, Name = "Company 1A Financials" };
            var company2 = new Company { Id = company2Id, TenantId = tenant2Id, Name = "Company 2B Financials" };

            var branch1 = new Branch { Id = branch1Id, TenantId = tenant1Id, CompanyId = company1Id, Name = "Branch 1A Main" };
            var branch2 = new Branch { Id = branch2Id, TenantId = tenant2Id, CompanyId = company2Id, Name = "Branch 2B Main" };

            seedContext.Tenants.AddRange(tenant1, tenant2);
            seedContext.Companies.AddRange(company1, company2);
            seedContext.Branches.AddRange(branch1, branch2);
            await seedContext.SaveChangesAsync();
        }

        // 2. Act: Query with Tenant 1 Context
        _tenantContext.SetContext(tenant1Id, company1Id, branch1Id);
        using (var tenant1Context = new TenancyDbContext(_options, _tenantContext))
        {
            var visibleCompanies = await tenant1Context.Companies.ToListAsync();
            var visibleBranches = await tenant1Context.Branches.ToListAsync();

            // 3. Assert: Only Tenant 1 data is visible! Tenant 2 is completely isolated!
            visibleCompanies.Should().HaveCount(1);
            visibleCompanies.Single().Id.Should().Be(company1Id);
            visibleCompanies.Single().Name.Should().Be("Company 1A Financials");

            visibleBranches.Should().HaveCount(1);
            visibleBranches.Single().Id.Should().Be(branch1Id);

            // Directly trying to find Company 2B by its Id returns null
            var unauthorizedCompany = await tenant1Context.Companies.FirstOrDefaultAsync(c => c.Id == company2Id);
            unauthorizedCompany.Should().BeNull();
        }

        // 4. Act: Switch to Tenant 2 Context
        _tenantContext.SetContext(tenant2Id, company2Id, branch2Id);
        using (var tenant2Context = new TenancyDbContext(_options, _tenantContext))
        {
            var visibleCompanies = await tenant2Context.Companies.ToListAsync();

            // 5. Assert: Only Tenant 2 data is visible! Tenant 1 is completely isolated!
            visibleCompanies.Should().HaveCount(1);
            visibleCompanies.Single().Id.Should().Be(company2Id);
            visibleCompanies.Single().Name.Should().Be("Company 2B Financials");

            var unauthorizedCompany = await tenant2Context.Companies.FirstOrDefaultAsync(c => c.Id == company1Id);
            unauthorizedCompany.Should().BeNull();
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
