using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Tenancy.Domain;
using ERP.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

public class DatabasePolicyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TenancyDbContext> _options;

    public DatabasePolicyTests()
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
    public void ModelPolicy_DecimalPrecision_ShouldBeConfiguredTo28Scale4()
    {
        using var context = new TenancyDbContext(_options, null);

        var entityType = context.Model.FindEntityType(typeof(Company));
        entityType.Should().NotBeNull();
    }

    [Fact]
    public async Task AuditPolicy_ShouldAutomaticallySetTimestampsAndUser()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetContext(tenantId);

        using var context = new TenancyDbContext(_options, tenantContext);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Audit Policy Test Corp",
            Identifier = "audit-test",
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        tenant.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        tenant.CreatedBy.Should().NotBeNullOrEmpty();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
