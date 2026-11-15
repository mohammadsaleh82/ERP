using ERP.Modules.Tenancy.Contracts.Interfaces;
using ERP.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Tenancy.Application.Services;

public class TenancyMembershipValidator : ITenancyMembershipValidator
{
    private readonly TenancyDbContext _dbContext;

    public TenancyMembershipValidator(TenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Memberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == userId &&
                           m.TenantId == tenantId &&
                           m.CompanyId == companyId &&
                           m.IsActive,
                       cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUserCompanyPermissionsAsync(
        Guid userId,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _dbContext.Memberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId &&
                                      m.TenantId == tenantId &&
                                      m.CompanyId == companyId &&
                                      m.IsActive,
                                 cancellationToken);

        if (membership is null || string.IsNullOrWhiteSpace(membership.Permissions))
        {
            return [];
        }

        return membership.Permissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
