namespace ERP.Modules.Tenancy.Contracts.Interfaces;

/// <summary>شرکت‌های یک کاربر بر اساس memberships فعال — منبع واقعی انتخاب شرکت در UI.</summary>
public interface ITenancyModuleApi
{
    Task<IReadOnlyList<UserTenantDto>> GetUserCompaniesAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record UserTenantDto(
    Guid TenantId,
    string TenantName,
    IReadOnlyList<UserCompanyDto> Companies);

public sealed record UserCompanyDto(
    Guid CompanyId,
    string Name,
    int ActiveFiscalYear,
    bool IsActive,
    bool MembershipActive);
