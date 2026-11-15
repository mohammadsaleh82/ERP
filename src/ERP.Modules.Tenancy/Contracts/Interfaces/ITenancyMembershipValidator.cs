namespace ERP.Modules.Tenancy.Contracts.Interfaces;

public interface ITenancyMembershipValidator
{
    Task<bool> HasActiveMembershipAsync(Guid userId, Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserCompanyPermissionsAsync(Guid userId, Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}
