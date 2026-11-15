using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Tenancy.Infrastructure.Persistence;

public class TenancyDbContext : BaseDbContext
{
    public const string Schema = "org";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<UserCompanyMembership> Memberships => Set<UserCompanyMembership>();

    public TenancyDbContext(
        DbContextOptions<TenancyDbContext> options,
        ITenantContext? tenantContext = null,
        ICurrentUserContext? currentUserContext = null,
        IClock? clock = null)
        : base(options, tenantContext, currentUserContext, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.ToTable("org_tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).HasMaxLength(150).IsRequired();
            b.Property(t => t.Identifier).HasMaxLength(50).IsRequired();
            b.HasIndex(t => t.Identifier).IsUnique();
        });

        builder.Entity<Company>(b =>
        {
            b.ToTable("org_companies");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(150).IsRequired();
            b.Property(c => c.NationalId).HasMaxLength(20);
            b.HasIndex(c => new { c.TenantId, c.NationalId });

            b.HasOne(c => c.Tenant)
             .WithMany(t => t.Companies)
             .HasForeignKey(c => c.TenantId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Branch>(b =>
        {
            b.ToTable("org_branches");
            b.HasKey(br => br.Id);
            b.Property(br => br.Name).HasMaxLength(150).IsRequired();
            b.Property(br => br.Code).HasMaxLength(30);

            b.HasOne(br => br.Company)
             .WithMany(c => c.Branches)
             .HasForeignKey(br => br.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Warehouse>(b =>
        {
            b.ToTable("org_warehouses");
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).HasMaxLength(150).IsRequired();
            b.Property(w => w.Code).HasMaxLength(30);

            b.HasOne(w => w.Branch)
             .WithMany(br => br.Warehouses)
             .HasForeignKey(w => w.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserCompanyMembership>(b =>
        {
            b.ToTable("org_user_memberships");
            b.HasKey(m => m.Id);
            b.HasIndex(m => new { m.UserId, m.TenantId, m.CompanyId }).IsUnique();
        });
    }
}
