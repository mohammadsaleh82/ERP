using ERP.Modules.Accounting.Application.Services.Audit;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.UnitTests;

public class ChartOfAccountsHierarchyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AccountingDbContext> _options;

    public ChartOfAccountsHierarchyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new AccountingDbContext(_options);
        initContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task Scenario01_CreateValidHierarchyTree_ShouldSucceed()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new AccountService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // 1. Level 1: Group
        var group = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(null, "1", "دارایی‌ها", "گروه دارایی", AccountNature.Asset, NormalBalance.Debit, AllowPosting: false));

        group.Level.Should().Be(AccountLevel.Group);
        group.FullCode.Should().Be("1");
        group.AllowPosting.Should().BeFalse();

        // 2. Level 2: General
        var general = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(group.Id, "10", "موجودی نقد و بانک", null, AccountNature.Asset, NormalBalance.Debit, AllowPosting: false));

        general.Level.Should().Be(AccountLevel.General);
        general.FullCode.Should().Be("110");
        general.ParentAccountId.Should().Be(group.Id);

        // 3. Level 3: Subsidiary
        var sub = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(general.Id, "10", "بانک‌ها", null, AccountNature.Asset, NormalBalance.Debit, AllowPosting: true));

        sub.Level.Should().Be(AccountLevel.Subsidiary);
        sub.FullCode.Should().Be("11010");
        sub.ParentAccountId.Should().Be(general.Id);

        // 4. Level 4: Detail
        var detail = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(sub.Id, "01", "بانک ملت شعبه مرکزی", null, AccountNature.Asset, NormalBalance.Debit, AllowPosting: true));

        detail.Level.Should().Be(AccountLevel.Detail);
        detail.FullCode.Should().Be("1101001");
        detail.ParentAccountId.Should().Be(sub.Id);

        // Assert full tree query
        var tree = await service.GetAccountTreeAsync(tenantId, companyId);
        tree.Should().ContainSingle();
        tree[0].Children.Should().ContainSingle();
        tree[0].Children[0].Children.Should().ContainSingle();
        tree[0].Children[0].Children[0].Children.Should().ContainSingle();
        tree[0].Children[0].Children[0].Children[0].Name.Should().Be("بانک ملت شعبه مرکزی");
    }

    [Fact]
    public async Task Scenario02_CreateWithInvalidParent_ShouldThrowException()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new AccountService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var nonExistentParentId = Guid.NewGuid();

        var action = async () => await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(nonExistentParentId, "10", "نامعتبر", null, AccountNature.Asset, NormalBalance.Debit));

        await action.Should().ThrowAsync<AccountingDomainException>()
            .WithMessage("*حساب والد*یافت نشد*");
    }

    [Fact]
    public void Scenario03_HierarchyCycle_SettingParentToOwnChild_ShouldThrowException()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var group = Account.CreateRoot(tenantId, companyId, "1", "دارایی‌ها", AccountNature.Asset);
        var general = group.CreateChild("10", "موجودی نقد و بانک");
        var sub = general.CreateChild("10", "بانک‌ها");

        // Attempt to make general a child of sub (Cycle: sub is a descendant of general!)
        var cycleAction = () => general.ChangeParent(sub);

        cycleAction.Should().Throw<AccountingDomainException>()
            .WithMessage("*چرخه*");
    }

    [Fact]
    public async Task Scenario04_DuplicateCodeInSameCompany_ShouldThrowException()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new AccountService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(null, "1", "دارایی‌ها", null, AccountNature.Asset, NormalBalance.Debit));

        var duplicateAction = async () => await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(null, "1", "دارایی‌های تکراری", null, AccountNature.Asset, NormalBalance.Debit));

        await duplicateAction.Should().ThrowAsync<AccountingDomainException>()
            .WithMessage("*قبلاً تعریف شده است*");
    }

    [Fact]
    public void Scenario05_CrossCompanyParent_ShouldThrowException()
    {
        var tenantId = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var parentInCompanyA = Account.CreateRoot(tenantId, companyA, "1", "دارایی شرکت الف", AccountNature.Asset);
        var accountInCompanyB = Account.CreateRoot(tenantId, companyB, "1", "دارایی شرکت ب", AccountNature.Asset);

        var action = () => accountInCompanyB.ChangeParent(parentInCompanyA);

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*یک شرکت*یکسان*");
    }

    [Fact]
    public void Scenario06_InactiveAccount_CannotBeUsedForPosting()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var group = Account.CreateRoot(tenantId, companyId, "1", "دارایی‌ها", AccountNature.Asset);
        var general = group.CreateChild("10", "نقد و بانک");
        var sub = general.CreateChild("10", "بانک سپه");

        sub.AllowPosting = true;
        sub.Deactivate();

        var action = () => sub.EnsureCanPost();

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*غیرفعال است*");
    }

    [Fact]
    public async Task Scenario07_DeleteAccountWithFinancialActivity_ShouldBeBlocked()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new AccountService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var group = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(null, "1", "دارایی‌ها", null, AccountNature.Asset, NormalBalance.Debit));
        var general = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(group.Id, "10", "نقد و بانک", null, AccountNature.Asset, NormalBalance.Debit));
        var sub = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(general.Id, "10", "بانک تجارت", null, AccountNature.Asset, NormalBalance.Debit, AllowPosting: true));
        var cash = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(general.Id, "20", "صندوق ریالی", null, AccountNature.Asset, NormalBalance.Debit, AllowPosting: true));

        // Seed a journal entry line for this account
        var document = new AccountingDocument(tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(), 1001, new DateOnly(2024, 5, 1), "سند تست");
        document.AddLine(sub.Id, DebitCredit.Debit, 1_000_000m, "واریز به حساب");
        document.AddLine(cash.Id, DebitCredit.Credit, 1_000_000m, "صندوق");

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        // Attempt to delete account with financial activity
        var deleteAction = async () => await service.DeleteAccountAsync(tenantId, companyId, sub.Id);

        await deleteAction.Should().ThrowAsync<AccountingDomainException>()
            .WithMessage("*گردش مالی*");
    }

    [Fact]
    public void Scenario08_PostingOnSummaryAccount_ShouldBeBlocked()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var group = Account.CreateRoot(tenantId, companyId, "1", "دارایی‌ها", AccountNature.Asset);
        var general = group.CreateChild("10", "نقد و بانک");

        // Group level cannot accept direct posting
        var groupAction = () => group.EnsureCanPost();
        groupAction.Should().Throw<AccountingDomainException>()
            .WithMessage("*تجمیعی و کنترلی*");

        // General level cannot accept direct posting
        var generalAction = () => general.EnsureCanPost();
        generalAction.Should().Throw<AccountingDomainException>()
            .WithMessage("*تجمیعی و کنترلی*");
    }

    [Fact]
    public void Scenario09_LevelDowngradeOnAccountWithChildren_ShouldBeBlocked()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var group = Account.CreateRoot(tenantId, companyId, "1", "دارایی‌ها", AccountNature.Asset);
        var general = group.CreateChild("10", "نقد و بانک");
        var sub = general.CreateChild("10", "بانک");

        // General has a child (sub). Attempting to change general to Detail (Level 4) should be blocked!
        var action = () => general.ChangeLevel(AccountLevel.Detail);

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*دارای زیرمجموعه است*");
    }

    [Fact]
    public async Task Scenario10_SearchAccountTreeAndBreadcrumbs_ShouldReturnOrderedHierarchy()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new AccountService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var group = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(null, "1", "دارایی‌های جاری", null, AccountNature.Asset, NormalBalance.Debit));
        var general = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(group.Id, "10", "موجودی نقد و بانک", null, AccountNature.Asset, NormalBalance.Debit));
        var sub = await service.CreateAccountAsync(tenantId, companyId,
            new CreateAccountRequest(general.Id, "10", "بانک ملی", null, AccountNature.Asset, NormalBalance.Debit, AllowPosting: true));

        // 1. Breadcrumb test
        var breadcrumbs = await service.GetParentsAsync(tenantId, companyId, sub.Id);
        breadcrumbs.Should().HaveCount(3);
        breadcrumbs[0].Name.Should().Be("دارایی‌های جاری");
        breadcrumbs[1].Name.Should().Be("موجودی نقد و بانک");
        breadcrumbs[2].Name.Should().Be("بانک ملی");

        // 2. Search test
        var searchResult = await service.SearchAccountsAsync(tenantId, companyId, new AccountFilterRequest(SearchTerm: "ملی"));
        searchResult.TotalCount.Should().Be(1);
        searchResult.Items[0].Name.Should().Be("بانک ملی");
    }

    [Fact]
    public async Task Scenario11_SeedTemplateToNewCompany_ShouldGenerateFullTreeWithoutAffectingExistingCompany()
    {
        using var dbContext = new AccountingDbContext(_options);
        var service = new AccountService(dbContext, new AccountingAuditTrailService(dbContext, new TestClock(), new TestUserContext(), new FixedCorrelationIdProvider()));

        var tenantId = Guid.NewGuid();
        var company1Id = Guid.NewGuid();
        var company2Id = Guid.NewGuid();

        // 1. Seed Company 1
        await service.SeedCompanyAccountsFromTemplateAsync(tenantId, company1Id);

        var company1Accounts = await dbContext.Accounts
            .Where(a => a.TenantId == tenantId && a.CompanyId == company1Id)
            .ToListAsync();

        company1Accounts.Should().HaveCountGreaterThan(20);
        company1Accounts.Any(a => a.FullCode == "1").Should().BeTrue();
        company1Accounts.Any(a => a.FullCode == "1010").Should().BeTrue();

        // 2. Seed Company 2 independently
        await service.SeedCompanyAccountsFromTemplateAsync(tenantId, company2Id);

        var company2Accounts = await dbContext.Accounts
            .Where(a => a.TenantId == tenantId && a.CompanyId == company2Id)
            .ToListAsync();

        company2Accounts.Should().HaveCount(company1Accounts.Count);

        // 3. Modifying Company 1 does not affect Company 2
        var bankCompany1 = company1Accounts.First(a => a.FullCode == "1010");
        bankCompany1.Name = "نام اختصاصی شرکت ۱";
        await dbContext.SaveChangesAsync();

        var bankCompany2 = await dbContext.Accounts
            .FirstAsync(a => a.TenantId == tenantId && a.CompanyId == company2Id && a.FullCode == "1010");
        bankCompany2.Name.Should().NotBe("نام اختصاصی شرکت ۱");
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
