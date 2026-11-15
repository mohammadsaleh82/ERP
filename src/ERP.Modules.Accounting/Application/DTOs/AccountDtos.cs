using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.DTOs;

public sealed record AccountDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? ParentAccountId,
    string Code,
    string FullCode,
    string Name,
    string? Description,
    AccountLevel Level,
    AccountNature Nature,
    NormalBalance NormalBalance,
    bool IsActive,
    bool IsSystemAccount,
    bool AllowPosting,
    bool HasChildren,
    bool RequireCostCenter,
    bool RequireProject,
    bool RequireDetailAccount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record AccountTreeNodeDto(
    Guid Id,
    Guid? ParentAccountId,
    string Code,
    string FullCode,
    string Name,
    AccountLevel Level,
    AccountNature Nature,
    NormalBalance NormalBalance,
    bool IsActive,
    bool AllowPosting,
    List<AccountTreeNodeDto> Children);

public sealed record AccountBreadcrumbDto(
    Guid Id,
    string Code,
    string FullCode,
    string Name,
    AccountLevel Level);

public sealed record CreateAccountRequest(
    Guid? ParentAccountId,
    string Code,
    string Name,
    string? Description,
    AccountNature? Nature,
    NormalBalance? NormalBalance,
    bool AllowPosting = true,
    bool RequireCostCenter = false,
    bool RequireProject = false,
    bool RequireDetailAccount = false);

public sealed record UpdateAccountRequest(
    string Name,
    string? Description,
    NormalBalance NormalBalance,
    bool RequireCostCenter,
    bool RequireProject,
    bool RequireDetailAccount);

public sealed record AccountFilterRequest(
    string? SearchTerm = null,
    AccountLevel? Level = null,
    AccountNature? Nature = null,
    bool? IsActive = null,
    bool? AllowPosting = null,
    Guid? ParentAccountId = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AccountTemplateDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsDefault,
    bool IsSystemTemplate,
    int TotalAccountsCount);

public sealed record AccountTemplateDetailDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsDefault,
    bool IsSystemTemplate,
    List<AccountTemplateLevelDto> Levels,
    List<AccountTemplateItemDto> Items);

public sealed record AccountTemplateLevelDto(
    AccountLevel Level,
    string Name,
    int CodeLength);

public sealed record AccountTemplateItemDto(
    Guid Id,
    string Code,
    string Name,
    string? ParentCode,
    AccountLevel Level,
    AccountNature Nature,
    NormalBalance NormalBalance,
    bool AllowPosting,
    string? Description);

public sealed record SeedTemplateRequest(
    Guid? TemplateId = null);
