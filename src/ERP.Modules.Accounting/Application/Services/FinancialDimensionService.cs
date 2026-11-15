using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IFinancialDimensionService
{
    /// <summary>ایجاد بعد مالی داخلی (تفصیلی، مرکز هزینه، پروژه، قرارداد، بعد سفارشی ...).</summary>
    Task<FinancialDimensionDto> CreateAsync(CreateFinancialDimensionCommand command, CancellationToken cancellationToken = default);

    /// <summary>ثبت/همگام‌سازی بعد مالی خارجی از ماژول مالک (Upsert بر اساس مرجع خارجی).</summary>
    Task<FinancialDimensionDto> RegisterExternalAsync(RegisterExternalDimensionCommand command, CancellationToken cancellationToken = default);

    Task<FinancialDimensionDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid dimensionId, CancellationToken cancellationToken = default);

    /// <summary>جستجوی بعد بر اساس مرجع خارجی ماژول مالک (بدون رفرنس به Entity مالک).</summary>
    Task<FinancialDimensionDto?> GetByExternalReferenceAsync(Guid tenantId, Guid companyId, FinancialDimensionType dimensionType, Guid externalReferenceId, CancellationToken cancellationToken = default);

    Task<FinancialDimensionDto> UpdateNameAsync(Guid tenantId, Guid companyId, Guid dimensionId, string name, string performedBy, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid tenantId, Guid companyId, Guid dimensionId, string performedBy, CancellationToken cancellationToken = default);

    Task ActivateAsync(Guid tenantId, Guid companyId, Guid dimensionId, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>فهرست صفحه‌بندی‌شده ابعاد با فیلتر نوع، وضعیت و جستجو.</summary>
    Task<(IReadOnlyList<FinancialDimensionDto> Items, int TotalCount)> ListAsync(Guid tenantId, Guid companyId, FinancialDimensionListQuery query, CancellationToken cancellationToken = default);

    /// <summary>تعریف/تغییر سیاست بعد مالی یک حساب (Upsert).</summary>
    Task<AccountDimensionPolicyDto> SetAccountPolicyAsync(SetAccountDimensionPolicyCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountDimensionPolicyDto>> GetAccountPoliciesAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>تعریف قاعده ترکیب ممنوع ابعاد.</summary>
    Task<DimensionCombinationRuleDto> AddCombinationRuleAsync(AddDimensionCombinationRuleCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DimensionCombinationRuleDto>> GetCombinationRulesAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Commands / Queries / DTOs
// ---------------------------------------------------------------------------

public sealed record CreateFinancialDimensionCommand(
    Guid TenantId,
    Guid CompanyId,
    FinancialDimensionType DimensionType,
    string Code,
    string Name,
    string PerformedBy);

public sealed record RegisterExternalDimensionCommand(
    Guid TenantId,
    Guid CompanyId,
    FinancialDimensionType DimensionType,
    Guid ExternalReferenceId,
    string Code,
    string Name,
    string OwnerModule,
    string PerformedBy);

public sealed class FinancialDimensionListQuery : BuildingBlocks.Common.PagedRequest
{
    public FinancialDimensionType? DimensionType { get; init; }
    public bool? IsActive { get; init; }
    public string? SearchTerm { get; init; }
}

public sealed record FinancialDimensionDto(
    Guid Id,
    FinancialDimensionType DimensionType,
    string Code,
    string Name,
    Guid? ExternalReferenceId,
    string? OwnerModule,
    bool IsActive);

public sealed record SetAccountDimensionPolicyCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid AccountId,
    FinancialDimensionType DimensionType,
    DimensionPolicyMode Mode,
    string PerformedBy);

public sealed record AccountDimensionPolicyDto(
    Guid Id,
    Guid AccountId,
    FinancialDimensionType DimensionType,
    DimensionPolicyMode Mode);

public sealed record AddDimensionCombinationRuleCommand(
    Guid TenantId,
    Guid CompanyId,
    FinancialDimensionType FirstDimensionType,
    Guid? FirstDimensionId,
    FinancialDimensionType SecondDimensionType,
    Guid? SecondDimensionId,
    string? Description,
    string PerformedBy);

public sealed record DimensionCombinationRuleDto(
    Guid Id,
    FinancialDimensionType FirstDimensionType,
    Guid? FirstDimensionId,
    FinancialDimensionType SecondDimensionType,
    Guid? SecondDimensionId,
    string? Description);

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------

public class FinancialDimensionService : IFinancialDimensionService
{
    private readonly AccountingDbContext _db;

    public FinancialDimensionService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<FinancialDimensionDto> CreateAsync(CreateFinancialDimensionCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureCodeIsUniqueAsync(command.TenantId, command.CompanyId, command.DimensionType, command.Code, null, cancellationToken);

        var dimension = FinancialDimension.Create(
            command.TenantId, command.CompanyId, command.DimensionType, command.Code, command.Name, command.PerformedBy);

        _db.FinancialDimensions.Add(dimension);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(dimension);
    }

    public async Task<FinancialDimensionDto> RegisterExternalAsync(RegisterExternalDimensionCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _db.FinancialDimensions
            .FirstOrDefaultAsync(d => d.TenantId == command.TenantId
                                      && d.CompanyId == command.CompanyId
                                      && d.DimensionType == command.DimensionType
                                      && d.ExternalReferenceId == command.ExternalReferenceId, cancellationToken);

        if (existing is not null)
        {
            // Upsert Idempotent: همگام‌سازی آینه مرجع با داده تازه ماژول مالک
            existing.SyncFromOwnerModule(command.Code, command.Name);
            await _db.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        await EnsureCodeIsUniqueAsync(command.TenantId, command.CompanyId, command.DimensionType, command.Code, null, cancellationToken);

        var dimension = FinancialDimension.Create(
            command.TenantId, command.CompanyId, command.DimensionType, command.Code, command.Name, command.PerformedBy,
            externalReferenceId: command.ExternalReferenceId,
            ownerModule: command.OwnerModule);

        _db.FinancialDimensions.Add(dimension);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(dimension);
    }

    public async Task<FinancialDimensionDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid dimensionId, CancellationToken cancellationToken = default)
    {
        var dimension = await FindAsync(tenantId, companyId, dimensionId, cancellationToken);
        return dimension is null ? null : ToDto(dimension);
    }

    public async Task<FinancialDimensionDto?> GetByExternalReferenceAsync(Guid tenantId, Guid companyId, FinancialDimensionType dimensionType, Guid externalReferenceId, CancellationToken cancellationToken = default)
    {
        var dimension = await _db.FinancialDimensions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId
                                      && d.CompanyId == companyId
                                      && d.DimensionType == dimensionType
                                      && d.ExternalReferenceId == externalReferenceId, cancellationToken);

        return dimension is null ? null : ToDto(dimension);
    }

    public async Task<FinancialDimensionDto> UpdateNameAsync(Guid tenantId, Guid companyId, Guid dimensionId, string name, string performedBy, CancellationToken cancellationToken = default)
    {
        var dimension = await FindAsync(tenantId, companyId, dimensionId, cancellationToken)
                        ?? throw new NotFoundException("FinancialDimension", dimensionId);

        dimension.UpdateName(name, performedBy);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(dimension);
    }

    public async Task DeactivateAsync(Guid tenantId, Guid companyId, Guid dimensionId, string performedBy, CancellationToken cancellationToken = default)
    {
        var dimension = await FindAsync(tenantId, companyId, dimensionId, cancellationToken)
                        ?? throw new NotFoundException("FinancialDimension", dimensionId);

        dimension.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid tenantId, Guid companyId, Guid dimensionId, string performedBy, CancellationToken cancellationToken = default)
    {
        var dimension = await FindAsync(tenantId, companyId, dimensionId, cancellationToken)
                        ?? throw new NotFoundException("FinancialDimension", dimensionId);

        dimension.Activate();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<FinancialDimensionDto> Items, int TotalCount)> ListAsync(Guid tenantId, Guid companyId, FinancialDimensionListQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _db.FinancialDimensions
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CompanyId == companyId);

        if (query.DimensionType.HasValue)
            dbQuery = dbQuery.Where(d => d.DimensionType == query.DimensionType.Value);

        if (query.IsActive.HasValue)
            dbQuery = dbQuery.Where(d => d.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            dbQuery = dbQuery.Where(d => d.Code.Contains(term) || d.Name.Contains(term));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderBy(d => d.DimensionType)
            .ThenBy(d => d.Code)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new FinancialDimensionDto(
                d.Id, d.DimensionType, d.Code, d.Name, d.ExternalReferenceId, d.OwnerModule, d.IsActive))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<AccountDimensionPolicyDto> SetAccountPolicyAsync(SetAccountDimensionPolicyCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == command.TenantId && a.CompanyId == command.CompanyId && a.Id == command.AccountId, cancellationToken)
            ?? throw new NotFoundException("Account", command.AccountId);

        // حساب‌های غیرقابل ثبت هم می‌توانند سیاست داشته باشند (تداوم قاعده از ریشه به معین)
        var policy = await _db.AccountDimensionPolicies
            .FirstOrDefaultAsync(p => p.TenantId == command.TenantId
                                      && p.CompanyId == command.CompanyId
                                      && p.AccountId == command.AccountId
                                      && p.DimensionType == command.DimensionType, cancellationToken);

        if (policy is not null)
        {
            policy.ChangeMode(command.Mode, command.PerformedBy);
        }
        else
        {
            policy = AccountDimensionPolicy.CreateOrReplace(
                command.TenantId, command.CompanyId, command.AccountId, command.DimensionType, command.Mode, command.PerformedBy);
            _db.AccountDimensionPolicies.Add(policy);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new AccountDimensionPolicyDto(policy.Id, policy.AccountId, policy.DimensionType, policy.Mode);
    }

    public async Task<IReadOnlyList<AccountDimensionPolicyDto>> GetAccountPoliciesAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var policies = await _db.AccountDimensionPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.CompanyId == companyId && p.AccountId == accountId)
            .OrderBy(p => p.DimensionType)
            .Select(p => new AccountDimensionPolicyDto(p.Id, p.AccountId, p.DimensionType, p.Mode))
            .ToListAsync(cancellationToken);

        return policies;
    }

    public async Task<DimensionCombinationRuleDto> AddCombinationRuleAsync(AddDimensionCombinationRuleCommand command, CancellationToken cancellationToken = default)
    {
        // اعتبار ابعاد سطح مقدار: باید در همین شرکت و از همان نوع اعلام‌شده باشند
        if (command.FirstDimensionId.HasValue)
        {
            await EnsureDimensionBelongsToCompanyAsync(command.TenantId, command.CompanyId, command.FirstDimensionId.Value, command.FirstDimensionType, cancellationToken);
        }

        if (command.SecondDimensionId.HasValue)
        {
            await EnsureDimensionBelongsToCompanyAsync(command.TenantId, command.CompanyId, command.SecondDimensionId.Value, command.SecondDimensionType, cancellationToken);
        }

        var rule = DimensionCombinationRule.CreateForbidden(
            command.TenantId, command.CompanyId,
            command.FirstDimensionType, command.FirstDimensionId,
            command.SecondDimensionType, command.SecondDimensionId,
            command.Description, command.PerformedBy);

        // جلوگیری از قاعده تکراری
        var duplicate = await _db.DimensionCombinationRules
            .AnyAsync(r => r.TenantId == command.TenantId
                           && r.CompanyId == command.CompanyId
                           && r.FirstDimensionType == rule.FirstDimensionType
                           && r.FirstDimensionId == rule.FirstDimensionId
                           && r.SecondDimensionType == rule.SecondDimensionType
                           && r.SecondDimensionId == rule.SecondDimensionId, cancellationToken)
            || await _db.DimensionCombinationRules
            .AnyAsync(r => r.TenantId == command.TenantId
                           && r.CompanyId == command.CompanyId
                           && r.FirstDimensionType == rule.SecondDimensionType
                           && r.FirstDimensionId == rule.SecondDimensionId
                           && r.SecondDimensionType == rule.FirstDimensionType
                           && r.SecondDimensionId == rule.FirstDimensionId, cancellationToken);

        if (duplicate)
            throw new AccountingDomainException("Accounting.DimensionCombinationRuleDuplicate",
                "قاعده ترکیب ممنوع مشابه از قبل برای این شرکت تعریف شده است.");

        _db.DimensionCombinationRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);

        return new DimensionCombinationRuleDto(rule.Id, rule.FirstDimensionType, rule.FirstDimensionId, rule.SecondDimensionType, rule.SecondDimensionId, rule.Description);
    }

    public async Task<IReadOnlyList<DimensionCombinationRuleDto>> GetCombinationRulesAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var rules = await _db.DimensionCombinationRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId)
            .OrderBy(r => r.FirstDimensionType)
            .ThenBy(r => r.SecondDimensionType)
            .Select(r => new DimensionCombinationRuleDto(r.Id, r.FirstDimensionType, r.FirstDimensionId, r.SecondDimensionType, r.SecondDimensionId, r.Description))
            .ToListAsync(cancellationToken);

        return rules;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<FinancialDimension?> FindAsync(Guid tenantId, Guid companyId, Guid dimensionId, CancellationToken cancellationToken) =>
        await _db.FinancialDimensions
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.CompanyId == companyId && d.Id == dimensionId, cancellationToken);

    private async Task EnsureCodeIsUniqueAsync(Guid tenantId, Guid companyId, FinancialDimensionType type, string code, Guid? excludingId, CancellationToken cancellationToken)
    {
        var query = _db.FinancialDimensions
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CompanyId == companyId && d.DimensionType == type && d.Code == code);

        if (excludingId.HasValue)
            query = query.Where(d => d.Id != excludingId.Value);

        if (await query.AnyAsync(cancellationToken))
        {
            throw new AccountingDomainException("Accounting.DimensionCodeDuplicate",
                $"کد بعد مالی '{code}' از قبل برای نوع '{type}' در این شرکت ثبت شده است.");
        }
    }

    private async Task EnsureDimensionBelongsToCompanyAsync(Guid tenantId, Guid companyId, Guid dimensionId, FinancialDimensionType expectedType, CancellationToken cancellationToken)
    {
        var dimension = await FindAsync(tenantId, companyId, dimensionId, cancellationToken)
                        ?? throw new NotFoundException("FinancialDimension", dimensionId);

        if (dimension.DimensionType != expectedType)
            throw new AccountingDomainException("Accounting.DimensionTypeMismatch",
                $"بعد مالی '{dimension.Code}' از نوع '{dimension.DimensionType}' است اما قاعده برای نوع '{expectedType}' تعریف شده.");
    }

    private static FinancialDimensionDto ToDto(FinancialDimension dimension) =>
        new(dimension.Id, dimension.DimensionType, dimension.Code, dimension.Name, dimension.ExternalReferenceId, dimension.OwnerModule, dimension.IsActive);
}

/// <summary>
/// پیاده‌سازی قرارداد بین‌ماژولی ابعاد مالی.
/// ماژول‌های مالک فقط این قرارداد را می‌شناسند؛ هیچ رفرنس به Entityهای داخلی حسابداری ندارند
/// و حسابداری نیز هیچ رفرنس به Entityهای مالک‌شان ندارد.
/// </summary>
public class FinancialDimensionApi : IFinancialDimensionApi
{
    private readonly IFinancialDimensionService _dimensionService;

    public FinancialDimensionApi(IFinancialDimensionService dimensionService)
    {
        _dimensionService = dimensionService;
    }

    public async Task<FinancialDimensionContractDto> RegisterAsync(RegisterFinancialDimensionRequest request, CancellationToken cancellationToken = default)
    {
        var dimension = await _dimensionService.RegisterExternalAsync(
            new RegisterExternalDimensionCommand(
                request.TenantId, request.CompanyId, request.DimensionType,
                request.ExternalReferenceId, request.Code, request.Name, request.OwnerModule, request.PerformedBy),
            cancellationToken);

        return ToContract(request.TenantId, request.CompanyId, dimension);
    }

    public async Task<FinancialDimensionContractDto> SyncAsync(SyncFinancialDimensionRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _dimensionService.GetByExternalReferenceAsync(
                request.TenantId, request.CompanyId, request.DimensionType, request.ExternalReferenceId, cancellationToken)
            ?? throw new NotFoundException("FinancialDimension", request.ExternalReferenceId);

        if (!string.IsNullOrWhiteSpace(request.Name))
            existing = await _dimensionService.UpdateNameAsync(request.TenantId, request.CompanyId, existing.Id, request.Name, request.PerformedBy, cancellationToken);

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
                await _dimensionService.ActivateAsync(request.TenantId, request.CompanyId, existing.Id, request.PerformedBy, cancellationToken);
            else
                await _dimensionService.DeactivateAsync(request.TenantId, request.CompanyId, existing.Id, request.PerformedBy, cancellationToken);
        }

        var refreshed = await _dimensionService.GetByIdAsync(request.TenantId, request.CompanyId, existing.Id, cancellationToken)
                        ?? throw new NotFoundException("FinancialDimension", existing.Id);

        return ToContract(request.TenantId, request.CompanyId, refreshed);
    }

    public async Task<bool> IsActiveAsync(Guid tenantId, Guid companyId, Domain.Enums.FinancialDimensionType dimensionType, Guid externalReferenceId, CancellationToken cancellationToken = default)
    {
        var dimension = await _dimensionService.GetByExternalReferenceAsync(tenantId, companyId, dimensionType, externalReferenceId, cancellationToken);
        return dimension?.IsActive ?? false;
    }

    public async Task<FinancialDimensionContractDto?> GetByExternalReferenceAsync(Guid tenantId, Guid companyId, Domain.Enums.FinancialDimensionType dimensionType, Guid externalReferenceId, CancellationToken cancellationToken = default)
    {
        var dimension = await _dimensionService.GetByExternalReferenceAsync(tenantId, companyId, dimensionType, externalReferenceId, cancellationToken);
        return dimension is null ? null : ToContract(tenantId, companyId, dimension);
    }

    private static FinancialDimensionContractDto ToContract(Guid tenantId, Guid companyId, FinancialDimensionDto dimension) =>
        new(dimension.Id, tenantId, companyId, dimension.DimensionType, dimension.Code, dimension.Name, dimension.ExternalReferenceId, dimension.OwnerModule, dimension.IsActive);
}
