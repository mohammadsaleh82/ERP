using System.Linq.Expressions;
using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ERP.BuildingBlocks.Persistence;

public abstract class BaseDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly ICurrentUserContext? _currentUserContext;
    private readonly IClock? _clock;

    protected BaseDbContext(
        DbContextOptions options,
        ITenantContext? tenantContext = null,
        ICurrentUserContext? currentUserContext = null,
        IClock? clock = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
        _clock = clock;
    }

    public Guid CurrentTenantId => _tenantContext?.TenantId ?? Guid.Empty;
    public Guid CurrentCompanyId => _tenantContext?.CompanyId ?? Guid.Empty;
    public Guid CurrentBranchId => _tenantContext?.BranchId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. UTC DateTime Value Converter
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Apply UTC Conversion
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
                // Apply Standard Decimal Precision (28, 4)
                else if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(28);
                    property.SetScale(4);
                }
            }

            // Apply Multi-Tenancy Global Query Filters
            ApplyTenantFilters(modelBuilder, entityType);
        }
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
    {
        var clrType = entityType.ClrType;

        if (typeof(IBranchScoped).IsAssignableFrom(clrType))
        {
            var parameter = Expression.Parameter(clrType, "e");
            var currentTenantProp = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var currentCompanyProp = Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId));
            var currentBranchProp = Expression.Property(Expression.Constant(this), nameof(CurrentBranchId));
            var emptyGuid = Expression.Constant(Guid.Empty);

            var tenantFilter = Expression.OrElse(
                Expression.Equal(currentTenantProp, emptyGuid),
                Expression.Equal(Expression.Property(parameter, nameof(ITenantScoped.TenantId)), currentTenantProp)
            );

            var companyFilter = Expression.OrElse(
                Expression.Equal(currentCompanyProp, emptyGuid),
                Expression.Equal(Expression.Property(parameter, nameof(ICompanyScoped.CompanyId)), currentCompanyProp)
            );

            var branchFilter = Expression.OrElse(
                Expression.Equal(currentBranchProp, emptyGuid),
                Expression.Equal(Expression.Property(parameter, nameof(IBranchScoped.BranchId)), currentBranchProp)
            );

            var body = Expression.AndAlso(tenantFilter, Expression.AndAlso(companyFilter, branchFilter));
            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
        else if (typeof(ICompanyScoped).IsAssignableFrom(clrType))
        {
            var parameter = Expression.Parameter(clrType, "e");
            var currentTenantProp = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var currentCompanyProp = Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId));
            var emptyGuid = Expression.Constant(Guid.Empty);

            var tenantFilter = Expression.OrElse(
                Expression.Equal(currentTenantProp, emptyGuid),
                Expression.Equal(Expression.Property(parameter, nameof(ITenantScoped.TenantId)), currentTenantProp)
            );

            var companyFilter = Expression.OrElse(
                Expression.Equal(currentCompanyProp, emptyGuid),
                Expression.Equal(Expression.Property(parameter, nameof(ICompanyScoped.CompanyId)), currentCompanyProp)
            );

            var body = Expression.AndAlso(tenantFilter, companyFilter);
            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
        else if (typeof(ITenantScoped).IsAssignableFrom(clrType))
        {
            var parameter = Expression.Parameter(clrType, "e");
            var currentTenantProp = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var emptyGuid = Expression.Constant(Guid.Empty);

            var body = Expression.OrElse(
                Expression.Equal(currentTenantProp, emptyGuid),
                Expression.Equal(Expression.Property(parameter, nameof(ITenantScoped.TenantId)), currentTenantProp)
            );

            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock?.UtcNow ?? DateTimeOffset.UtcNow;
        var userId = _currentUserContext?.UserId?.ToString() ?? "system";

        // 1. Audit Trail Stamps & Concurrency Versioning
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.LastModifiedAtUtc = now;
                    auditable.LastModifiedBy = userId;
                }
            }

            var versionProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Version" && p.Metadata.ClrType == typeof(uint));
            if (versionProp != null)
            {
                if (entry.State == EntityState.Added)
                {
                    versionProp.CurrentValue = 1u;
                }
                else if (entry.State == EntityState.Modified)
                {
                    var cur = (uint)(versionProp.CurrentValue ?? 1u);
                    versionProp.CurrentValue = cur + 1u;
                }
            }
        }

        // Strict Tenant / Company / Branch Mutation Guards (Defense-in-depth on Add, Update, Delete)
        if (CurrentTenantId != Guid.Empty)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                {
                    if (entry.Entity.TenantId != Guid.Empty && entry.Entity.TenantId != CurrentTenantId)
                    {
                        throw new System.Security.SecurityException(
                            $"Tenant violation: cannot {entry.State} entity with TenantId '{entry.Entity.TenantId}' under active tenant '{CurrentTenantId}'.");
                    }
                }
            }
        }

        if (CurrentCompanyId != Guid.Empty)
        {
            foreach (var entry in ChangeTracker.Entries<ICompanyScoped>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                {
                    if (entry.Entity.CompanyId != Guid.Empty && entry.Entity.CompanyId != CurrentCompanyId)
                    {
                        throw new System.Security.SecurityException(
                            $"Company violation: cannot {entry.State} entity with CompanyId '{entry.Entity.CompanyId}' under active company '{CurrentCompanyId}'.");
                    }
                }
            }
        }

        if (CurrentBranchId != Guid.Empty)
        {
            foreach (var entry in ChangeTracker.Entries<IBranchScoped>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                {
                    if (entry.Entity.BranchId != Guid.Empty && entry.Entity.BranchId != CurrentBranchId)
                    {
                        throw new System.Security.SecurityException(
                            $"Branch violation: cannot {entry.State} entity with BranchId '{entry.Entity.BranchId}' under active branch '{CurrentBranchId}'.");
                    }
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
