using ERP.Modules.Tenancy.Contracts.Interfaces;
using ERP.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Tenancy.Application.Services;

/// <summary>خواندن زمینه شرکت‌های کاربر از memberships — با حفظ معماری module-boundary.</summary>
public class TenancyModuleApi(TenancyDbContext dbContext) : ITenancyModuleApi
{
    public async Task<IReadOnlyList<UserTenantDto>> GetUserCompaniesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(dbContext.Companies.AsNoTracking(),
                m => m.CompanyId,
                c => c.Id,
                (m, c) => new { m.TenantId, m.CompanyId, c.Name, c.ActiveFiscalYear, c.IsActive, m.IsActive })
            .Join(dbContext.Tenants.AsNoTracking(),
                x => x.TenantId,
                t => t.Id,
                (x, t) => new { x.TenantId, TenantName = t.Name, x.CompanyId, x.Name, x.ActiveFiscalYear, x.IsActive, x.IsActiveMembership = x.IsActive })
            .Where(x => x.IsActiveMembership)
            .OrderBy(x => x.TenantName)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => (x.TenantId, x.TenantName))
            .Select(g => new UserTenantDto(
                g.Key.TenantId,
                g.Key.TenantName,
                g.Select(x => new UserCompanyDto(
                    x.CompanyId,
                    x.Name,
                    x.ActiveFiscalYear,
                    x.IsActive,
                    x.IsActiveMembership)).ToList()))
            .ToList();
    }
}
