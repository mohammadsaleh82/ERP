using ERP.BuildingBlocks.Common;
using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using ERP.Modules.Accounting.Application.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IAccountService
{
    Task<AccountDto> CreateAccountAsync(Guid tenantId, Guid companyId, CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto> UpdateAccountAsync(Guid tenantId, Guid companyId, Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAccountAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default);
    Task ActivateAccountAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default);

    Task<AccountDto> GetAccountByIdAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default);
    Task<AccountDto> GetAccountByCodeAsync(Guid tenantId, Guid companyId, string fullCode, CancellationToken cancellationToken = default);
    Task<List<AccountTreeNodeDto>> GetAccountTreeAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<List<AccountDto>> GetChildrenAsync(Guid tenantId, Guid companyId, Guid parentAccountId, CancellationToken cancellationToken = default);
    Task<List<AccountBreadcrumbDto>> GetParentsAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountDto>> SearchAccountsAsync(Guid tenantId, Guid companyId, AccountFilterRequest request, CancellationToken cancellationToken = default);
    Task<List<AccountDto>> GetPostableAccountsAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);

    Task<List<AccountTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<AccountTemplateDetailDto> GetTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task SeedCompanyAccountsFromTemplateAsync(Guid tenantId, Guid companyId, Guid? templateId = null, CancellationToken cancellationToken = default);
}

public class AccountService : IAccountService
{
    private readonly AccountingDbContext _dbContext;
    private readonly IAccountingAuditTrailService _auditTrail;

    public AccountService(AccountingDbContext dbContext, IAccountingAuditTrailService auditTrail)
    {
        _dbContext = dbContext;
        _auditTrail = auditTrail;
    }

    public async Task<AccountDto> CreateAccountAsync(
        Guid tenantId,
        Guid companyId,
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        Account account;

        if (request.ParentAccountId.HasValue && request.ParentAccountId.Value != Guid.Empty)
        {
            var parent = await _dbContext.Accounts
                .Include(a => a.SubAccounts)
                .FirstOrDefaultAsync(a => a.Id == request.ParentAccountId.Value && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

            if (parent is null)
            {
                throw new AccountingDomainException("Accounting.ParentAccountNotFound",
                    $"حساب والد با شناسه '{request.ParentAccountId.Value}' در شرکت جاری یافت نشد.");
            }

            var fullCode = $"{parent.FullCode}{request.Code.Trim()}";
            var exists = await _dbContext.Accounts
                .AnyAsync(a => a.TenantId == tenantId && a.CompanyId == companyId && a.FullCode == fullCode, cancellationToken);

            if (exists)
            {
                throw new AccountingDomainException("Accounting.DuplicateAccountCode",
                    $"کد حساب '{fullCode}' در این شرکت قبلاً تعریف شده است.");
            }

            account = parent.CreateChild(
                request.Code,
                request.Name,
                request.Description,
                isSystemAccount: false,
                request.NormalBalance,
                request.AllowPosting);

            account.RequireCostCenter = request.RequireCostCenter;
            account.RequireProject = request.RequireProject;
            account.RequireDetailAccount = request.RequireDetailAccount;

            _dbContext.Accounts.Add(account);
        }
        else
        {
            // ایجاد حساب ریشه (سطح گروه حساب)
            var fullCode = request.Code.Trim();
            var exists = await _dbContext.Accounts
                .AnyAsync(a => a.TenantId == tenantId && a.CompanyId == companyId && a.FullCode == fullCode, cancellationToken);

            if (exists)
            {
                throw new AccountingDomainException("Accounting.DuplicateAccountCode",
                    $"کد حساب '{fullCode}' در این شرکت قبلاً تعریف شده است.");
            }

            var nature = request.Nature ?? AccountNature.Asset;

            account = Account.CreateRoot(
                tenantId,
                companyId,
                request.Code,
                request.Name,
                nature,
                request.NormalBalance,
                request.Description);

            account.RequireCostCenter = request.RequireCostCenter;
            account.RequireProject = request.RequireProject;
            account.RequireDetailAccount = request.RequireDetailAccount;

            _dbContext.Accounts.Add(account);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditTrail.RecordAsync(
            tenantId, companyId, AuditTrailActions.AccountCreated,
            "Account", account.Id,
            after: AccountSnapshot(account),
            branchId: null, cancellationToken: cancellationToken);

        return MapToDto(account, hasChildren: false);
    }

    public async Task<AccountDto> UpdateAccountAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        UpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Account", accountId);
        }

        account.Update(
            request.Name,
            request.Description,
            request.NormalBalance,
            request.RequireCostCenter,
            request.RequireProject,
            request.RequireDetailAccount);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditTrail.RecordAsync(
            tenantId, companyId, AuditTrailActions.AccountUpdated,
            "Account", account.Id,
            after: AccountSnapshot(account),
            branchId: null, cancellationToken: cancellationToken);

        return MapToDto(account, account.SubAccounts.Count > 0);
    }

    public async Task DeactivateAccountAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Account", accountId);
        }

        account.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditTrail.RecordAsync(
            tenantId, companyId, AuditTrailActions.AccountDeactivated,
            "Account", account.Id,
            after: AccountSnapshot(account),
            branchId: null, cancellationToken: cancellationToken);
    }

    public async Task ActivateAccountAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Account", accountId);
        }

        account.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Account", accountId);
        }

        if (account.IsSystemAccount)
        {
            throw new AccountingDomainException("Accounting.CannotDeleteSystemAccount",
                "حساب‌های سیستمی و پایه الگو قابل حذف نمی‌باشند. در صورت عدم نیاز می‌توانید حساب را غیرفعال فرمایید.");
        }

        if (account.SubAccounts.Count > 0)
        {
            throw new AccountingDomainException("Accounting.CannotDeleteAccountWithChildren",
                $"حساب '{account.Name}' دارای {account.SubAccounts.Count} زیرحساب است و امکان حذف مستقیم آن وجود ندارد. ابتدا زیرحساب‌ها را حذف یا منتقل نمایید.");
        }

        // بررسی گردش مالی در اسناد حسابداری
        var hasActivity = await _dbContext.DocumentLines
            .AnyAsync(l => l.AccountId == accountId && l.TenantId == tenantId && l.CompanyId == companyId, cancellationToken);

        if (hasActivity)
        {
            throw new AccountingDomainException("Accounting.CannotDeleteAccountWithFinancialActivity",
                $"حساب '{account.Name}' ({account.FullCode}) دارای گردش مالی و اسناد حسابداری ثبت‌شده است و طبق قوانین مالیاتی و حسابرسی حذف آن اکیداً ممنوع است. لطفاً آن را غیرفعال نمایید.");
        }

        _dbContext.Accounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountDto> GetAccountByIdAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Account", accountId);
        }

        return MapToDto(account, account.SubAccounts.Count > 0);
    }

    public async Task<AccountDto> GetAccountByCodeAsync(Guid tenantId, Guid companyId, string fullCode, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .Include(a => a.SubAccounts)
            .FirstOrDefaultAsync(a => a.FullCode == fullCode.Trim() && a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Account", fullCode);
        }

        return MapToDto(account, account.SubAccounts.Count > 0);
    }

    public async Task<List<AccountTreeNodeDto>> GetAccountTreeAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.Accounts
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .OrderBy(a => a.FullCode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var nodeLookup = accounts.ToDictionary(
            a => a.Id,
            a => new AccountTreeNodeDto(
                a.Id,
                a.ParentAccountId,
                a.Code,
                a.FullCode,
                a.Name,
                a.Level,
                a.Nature,
                a.NormalBalance,
                a.IsActive,
                a.AllowPosting,
                new List<AccountTreeNodeDto>()));

        var rootNodes = new List<AccountTreeNodeDto>();

        foreach (var account in accounts)
        {
            var node = nodeLookup[account.Id];
            if (account.ParentAccountId.HasValue && nodeLookup.TryGetValue(account.ParentAccountId.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes;
    }

    public async Task<List<AccountDto>> GetChildrenAsync(Guid tenantId, Guid companyId, Guid parentAccountId, CancellationToken cancellationToken = default)
    {
        var children = await _dbContext.Accounts
            .Where(a => a.ParentAccountId == parentAccountId && a.TenantId == tenantId && a.CompanyId == companyId)
            .Include(a => a.SubAccounts)
            .OrderBy(a => a.FullCode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return children.Select(c => MapToDto(c, c.SubAccounts.Count > 0)).ToList();
    }

    public async Task<List<AccountBreadcrumbDto>> GetParentsAsync(Guid tenantId, Guid companyId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var allCompanyAccounts = await _dbContext.Accounts
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var accountMap = allCompanyAccounts.ToDictionary(a => a.Id);

        if (!accountMap.TryGetValue(accountId, out var current))
        {
            throw new NotFoundException("Account", accountId);
        }

        var path = new List<AccountBreadcrumbDto>();

        while (current != null)
        {
            path.Add(new AccountBreadcrumbDto(current.Id, current.Code, current.FullCode, current.Name, current.Level));
            current = current.ParentAccountId.HasValue && accountMap.TryGetValue(current.ParentAccountId.Value, out var parent)
                ? parent
                : null;
        }

        path.Reverse();
        return path;
    }

    public async Task<PagedResult<AccountDto>> SearchAccountsAsync(
        Guid tenantId,
        Guid companyId,
        AccountFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Accounts
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Include(a => a.SubAccounts)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(a => a.Name.Contains(term) || a.FullCode.Contains(term) || a.Code.Contains(term));
        }

        if (request.Level.HasValue)
        {
            query = query.Where(a => a.Level == request.Level.Value);
        }

        if (request.Nature.HasValue)
        {
            query = query.Where(a => a.Nature == request.Nature.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(a => a.IsActive == request.IsActive.Value);
        }

        if (request.AllowPosting.HasValue)
        {
            query = query.Where(a => a.AllowPosting == request.AllowPosting.Value);
        }

        if (request.ParentAccountId.HasValue)
        {
            query = query.Where(a => a.ParentAccountId == request.ParentAccountId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : (request.PageSize > 200 ? 200 : request.PageSize);

        var items = await query
            .OrderBy(a => a.FullCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(a => MapToDto(a, a.SubAccounts.Count > 0)).ToList();

        return new PagedResult<AccountDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<List<AccountDto>> GetPostableAccountsAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var postable = await _dbContext.Accounts
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId && a.IsActive && a.AllowPosting && !a.SubAccounts.Any())
            .OrderBy(a => a.FullCode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return postable.Select(a => MapToDto(a, hasChildren: false)).ToList();
    }

    public async Task<List<AccountTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _dbContext.AccountTemplates
            .Include(t => t.Items)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            // اگر دیتابیس خالی است، الگوی پیش‌فرض را ارزیابی و برمی‌گردانیم
            var defaultTemplate = DefaultIranianCommercialTemplate.Create();
            return [new AccountTemplateDto(
                defaultTemplate.Id,
                defaultTemplate.Title,
                defaultTemplate.Description,
                defaultTemplate.IsDefault,
                defaultTemplate.IsSystemTemplate,
                defaultTemplate.Items.Count)];
        }

        return templates.Select(t => new AccountTemplateDto(
            t.Id,
            t.Title,
            t.Description,
            t.IsDefault,
            t.IsSystemTemplate,
            t.Items.Count)).ToList();
    }

    public async Task<AccountTemplateDetailDto> GetTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.AccountTemplates
            .Include(t => t.Levels)
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template is null)
        {
            if (templateId == DefaultIranianCommercialTemplate.DefaultTemplateId)
            {
                template = DefaultIranianCommercialTemplate.Create();
            }
            else
            {
                throw new NotFoundException("AccountTemplate", templateId);
            }
        }

        return new AccountTemplateDetailDto(
            template.Id,
            template.Title,
            template.Description,
            template.IsDefault,
            template.IsSystemTemplate,
            template.Levels.OrderBy(l => (int)l.Level).Select(l => new AccountTemplateLevelDto(l.Level, l.Name, l.CodeLength)).ToList(),
            template.Items.OrderBy(i => i.SortOrder).Select(i => new AccountTemplateItemDto(
                i.Id,
                i.Code,
                i.Name,
                i.ParentCode,
                i.Level,
                i.Nature,
                i.NormalBalance,
                i.AllowPosting,
                i.Description)).ToList());
    }

    public async Task SeedCompanyAccountsFromTemplateAsync(
        Guid tenantId,
        Guid companyId,
        Guid? templateId = null,
        CancellationToken cancellationToken = default)
    {
        var existingCount = await _dbContext.Accounts
            .CountAsync(a => a.TenantId == tenantId && a.CompanyId == companyId, cancellationToken);

        if (existingCount > 0)
        {
            throw new AccountingDomainException("Accounting.CompanyAlreadyHasAccounts",
                "این شرکت دارای کدینگ حساب‌های از پیش تعریف‌شده است و امکان اعمال مجدد الگو وجود ندارد.");
        }

        AccountTemplate template;
        if (templateId.HasValue && templateId.Value != Guid.Empty && templateId.Value != DefaultIranianCommercialTemplate.DefaultTemplateId)
        {
            template = await _dbContext.AccountTemplates
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == templateId.Value, cancellationToken)
                ?? throw new NotFoundException("AccountTemplate", templateId.Value);
        }
        else
        {
            template = DefaultIranianCommercialTemplate.Create();
        }

        // دیکشنری کدها به اکانت‌های ساخته‌شده جدید در دیتابیس شرکت
        var codeToAccount = new Dictionary<string, Account>();

        // ایجاد بر اساس ترتیب سطوح: گروه (1) -> کل (2) -> معین (3) -> تفصیلی (4)
        var orderedItems = template.Items.OrderBy(i => (int)i.Level).ThenBy(i => i.SortOrder).ToList();

        foreach (var item in orderedItems)
        {
            if (string.IsNullOrWhiteSpace(item.ParentCode))
            {
                // حساب سطح ۱ (گروه)
                var rootAccount = Account.CreateRoot(
                    tenantId,
                    companyId,
                    item.Code,
                    item.Name,
                    item.Nature,
                    item.NormalBalance,
                    item.Description,
                    isSystemAccount: true);

                _dbContext.Accounts.Add(rootAccount);
                codeToAccount[item.Code] = rootAccount;
            }
            else
            {
                if (!codeToAccount.TryGetValue(item.ParentCode, out var parentAccount))
                {
                    throw new AccountingDomainException("Accounting.TemplateHierarchyBroken",
                        $"کد والد '{item.ParentCode}' برای حساب '{item.Name}' ({item.Code}) در الگو یافت نشد.");
                }

                // محاسبه کد نسبی فرزند نسبت به والد
                var relativeCode = item.Code.StartsWith(parentAccount.FullCode)
                    ? item.Code[parentAccount.FullCode.Length..]
                    : item.Code;

                var childAccount = parentAccount.CreateChild(
                    relativeCode,
                    item.Name,
                    item.Description,
                    isSystemAccount: true,
                    item.NormalBalance,
                    item.AllowPosting);

                _dbContext.Accounts.Add(childAccount);
                codeToAccount[item.Code] = childAccount;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AccountDto MapToDto(Account a, bool hasChildren) =>
        new(
            a.Id,
            a.TenantId,
            a.CompanyId,
            a.ParentAccountId,
            a.Code,
            a.FullCode,
            a.Name,
            a.Description,
            a.Level,
            a.Nature,
            a.NormalBalance,
            a.IsActive,
            a.IsSystemAccount,
            a.AllowPosting,
            hasChildren,
            a.RequireCostCenter,
            a.RequireProject,
            a.RequireDetailAccount,
            a.CreatedAtUtc,
            a.LastModifiedAtUtc);

    private static Dictionary<string, object?> AccountSnapshot(Account account) => new()
    {
        ["fullCode"] = account.FullCode,
        ["name"] = account.Name,
        ["nature"] = account.Nature.ToString(),
        ["isActive"] = account.IsActive,
        ["allowPosting"] = account.AllowPosting,
        ["version"] = (long)account.Version
    };

}
