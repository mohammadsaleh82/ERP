using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
using FluentAssertions;
using Xunit;
using Dv = ERP.Modules.Accounting.Domain.Services.DimensionValidationService;

namespace ERP.UnitTests;

/// <summary>
/// تست‌های خالص موتور قواعد ابعاد مالی:
/// تفصیلی اجباری، انواع مجاز، انواع ممنوع، شرکت دیگر، بعد غیرفعال، نوع ناهمخوان و ترکیب ممنوع.
/// </summary>
public class DimensionValidationTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    private static Dv.DimensionMeta Dim(Guid id, FinancialDimensionType type, Guid company = default, bool active = true) =>
        new(id, type, company == default ? CompanyA : company, active, "D-" + id.ToString()[..6]);

    private static Dv.AccountDimensionMeta AccountMeta(params (FinancialDimensionType, DimensionPolicyMode)[] policies) =>
        new(Guid.NewGuid(), "1001", false, false, false, policies.ToList());

    private static Dv.LineDimensionInput Line(Guid accountId, int lineNumber, params (FinancialDimensionType, Guid)[] assignments) =>
        new(lineNumber, accountId, assignments.ToList());

    private static Guid Id() => Guid.NewGuid();

    // ------------------------------------------------------------------
    // سیاست اجباری
    // ------------------------------------------------------------------

    [Fact]
    public void RequiredDimension_MissingOnLine_ShouldBeRejected()
    {
        var account = AccountMeta((FinancialDimensionType.DetailAccount, DimensionPolicyMode.Required));
        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1)],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta>(),
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionRequired");
    }

    [Fact]
    public void RequiredDimension_PresentOnLine_ShouldPass()
    {
        var account = AccountMeta((FinancialDimensionType.DetailAccount, DimensionPolicyMode.Required));
        var detailId = Id();

        var act = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.DetailAccount, detailId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [detailId] = Dim(detailId, FinancialDimensionType.DetailAccount) },
            CompanyA, []);

        act.Should().NotThrow();
    }

    [Fact]
    public void LegacyRequireFlags_ShouldActAsRequiredPolicy()
    {
        var account = new Dv.AccountDimensionMeta(Id(), "1001", RequireCostCenter: true, RequireProject: false, RequireDetailAccount: false, []);
        var costCenterId = Id();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1)],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta>(),
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionRequired");
    }

    // ------------------------------------------------------------------
    // انواع ممنوع و لیست سفید
    // ------------------------------------------------------------------

    [Fact]
    public void ForbiddenDimensionType_ShouldBeRejected()
    {
        var account = AccountMeta((FinancialDimensionType.Project, DimensionPolicyMode.Forbidden));
        var projectId = Id();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.Project, projectId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [projectId] = Dim(projectId, FinancialDimensionType.Project) },
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionNotAllowed");
    }

    [Fact]
    public void AllowedWhitelist_ShouldRejectUnlistedTypes()
    {
        var account = AccountMeta((FinancialDimensionType.CostCenter, DimensionPolicyMode.Allowed));
        var employeeId = Id();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.Employee, employeeId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [employeeId] = Dim(employeeId, FinancialDimensionType.Employee) },
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionNotInAllowedSet");
    }

    [Fact]
    public void WhitelistedType_ShouldPass()
    {
        var account = AccountMeta((FinancialDimensionType.CostCenter, DimensionPolicyMode.Allowed));
        var costCenterId = Id();

        var act = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.CostCenter, costCenterId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [costCenterId] = Dim(costCenterId, FinancialDimensionType.CostCenter) },
            CompanyA, []);

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // بعد غیرفعال، شرکت دیگر و نوع ناهمخوان
    // ------------------------------------------------------------------

    [Fact]
    public void InactiveDimension_ShouldBeRejected()
    {
        var dimensionId = Id();
        var account = AccountMeta();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.CostCenter, dimensionId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [dimensionId] = Dim(dimensionId, FinancialDimensionType.CostCenter, active: false) },
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionInactive");
    }

    [Fact]
    public void DimensionOfAnotherCompany_ShouldBeRejected()
    {
        var dimensionId = Id();
        var account = AccountMeta();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.CostCenter, dimensionId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [dimensionId] = Dim(dimensionId, FinancialDimensionType.CostCenter, company: CompanyB) },
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionForeignCompany");
    }

    [Fact]
    public void UnknownDimension_ShouldBeRejectedAsNotFound()
    {
        var account = AccountMeta();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.DetailAccount, Guid.NewGuid()))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta>(),
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionNotFound");
    }

    [Fact]
    public void TypeMismatchBetweenAssignmentAndDimensionRow_ShouldBeRejected()
    {
        var dimensionId = Id();
        var account = AccountMeta();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.Project, dimensionId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta> { [dimensionId] = Dim(dimensionId, FinancialDimensionType.CostCenter) },
            CompanyA, []);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionTypeMismatch");
    }

    // ------------------------------------------------------------------
    // ترکیب ممنوع
    // ------------------------------------------------------------------

    [Fact]
    public void ForbiddenCombinationAtTypeLevel_ShouldBeRejectedInAnyOrder()
    {
        var costCenterId = Id();
        var projectId = Id();
        var rule = new Dv.CombinationRuleInput(FinancialDimensionType.CostCenter, null, FinancialDimensionType.Project, null);
        var account = AccountMeta();

        var action = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.Project, projectId), (FinancialDimensionType.CostCenter, costCenterId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta>
            {
                [costCenterId] = Dim(costCenterId, FinancialDimensionType.CostCenter),
                [projectId] = Dim(projectId, FinancialDimensionType.Project)
            },
            CompanyA, [rule]);

        action.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionCombinationForbidden");
    }

    [Fact]
    public void ForbiddenCombinationAtValueLevel_ShouldRejectOnlyThatPair()
    {
        var cc1 = Id();
        var cc2 = Id();
        var project1 = Id();
        var project2 = Id();
        var account = AccountMeta();
        var rule = new Dv.CombinationRuleInput(FinancialDimensionType.CostCenter, cc1, FinancialDimensionType.Project, project1);

        var dims = new Dictionary<Guid, Dv.DimensionMeta>
        {
            [cc1] = Dim(cc1, FinancialDimensionType.CostCenter),
            [cc2] = Dim(cc2, FinancialDimensionType.CostCenter),
            [project1] = Dim(project1, FinancialDimensionType.Project),
            [project2] = Dim(project2, FinancialDimensionType.Project)
        };

        var forbidden = () => Dv.ValidateLines(
            [Line(account.AccountId, 1, (FinancialDimensionType.CostCenter, cc1), (FinancialDimensionType.Project, project1))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            dims, CompanyA, [rule]);
        forbidden.Should().Throw<AccountingDomainException>().Which.Code.Should().Be("Accounting.DimensionCombinationForbidden");

        var allowed = () => Dv.ValidateLines(
            [Line(account.AccountId, 2, (FinancialDimensionType.CostCenter, cc2), (FinancialDimensionType.Project, project2))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            dims, CompanyA, [rule]);
        allowed.Should().NotThrow("جفت‌های دیگر از همان انواع مجازند");
    }

    [Fact]
    public void MultipleRequiredTypes_AllPresent_ShouldPass()
    {
        var account = AccountMeta(
            (FinancialDimensionType.DetailAccount, DimensionPolicyMode.Required),
            (FinancialDimensionType.CostCenter, DimensionPolicyMode.Required));
        var detailId = Id();
        var ccId = Id();

        var act = () => Dv.ValidateLines(
            [Line(account.AccountId, 1,
                (FinancialDimensionType.DetailAccount, detailId),
                (FinancialDimensionType.CostCenter, ccId))],
            new Dictionary<Guid, Dv.AccountDimensionMeta> { [account.AccountId] = account },
            new Dictionary<Guid, Dv.DimensionMeta>
            {
                [detailId] = Dim(detailId, FinancialDimensionType.DetailAccount),
                [ccId] = Dim(ccId, FinancialDimensionType.CostCenter)
            },
            CompanyA, []);

        act.Should().NotThrow("ترکیب چند بعد اجباری وقتی کامل باشد معتبر است");
    }
}
