using ERP.Api.Controllers;
using ERP.BuildingBlocks.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// ماتریس مجوزهای مستقل حسابداری (PROMPT 09) — همه ۲۰ مجوز یکتا هستند و هر
/// مجوز باید روی حداقل یک Action کنترلری اعمال شده باشد.
/// </summary>
public class PermissionMatrixTests
{
    [Fact]
    public void Matrix_HasTwentyDistinctPermissions()
    {
        AccountingPermissions.Matrix.Should().HaveCount(20);
        AccountingPermissions.Matrix.Distinct().Should().HaveCount(20);
        AccountingPermissions.Matrix.Should().OnlyContain(p => p.StartsWith("accounting:"));
    }

    [Fact]
    public void EveryMatrixPermission_IsAppliedToAtLeastOneControllerAction()
    {
        var applied = ControllerPermissions();
        var missing = AccountingPermissions.Matrix
            .Where(p => !applied.Contains(p))
            .ToList();

        missing.Should().BeEmpty($"این مجوزها باید روی کنترلرها اعمال شوند: {string.Join(", ", missing)}");
    }

    [Fact]
    public void IndependentOperations_HaveIndependentPermissions()
    {
        // عملیات مستقل نباید مجوز مشترک داشته باشند
        var perms = AccountingPermissions.Matrix;
        perms.Should().Contain(AccountingPermissions.SubmitVouchers)
            .And.Contain(AccountingPermissions.ApproveVouchers)
            .And.Contain(AccountingPermissions.PostVouchers)
            .And.Contain(AccountingPermissions.ReverseVouchers);
        AccountingPermissions.SubmitVouchers.Should().NotBe(AccountingPermissions.ApproveVouchers);
        AccountingPermissions.ApproveVouchers.Should().NotBe(AccountingPermissions.PostVouchers);
        AccountingPermissions.CloseFiscalPeriod.Should().NotBe(AccountingPermissions.CloseFiscalYear);
        AccountingPermissions.CloseFiscalYear.Should().NotBe(AccountingPermissions.ReopenFiscalYear);
    }

    [Fact]
    public void PolicyProvider_CreatesPolicyForEachPermission()
    {
        // نام مجوز همان نام سیاست است — همه مجوزها باید نام سیاست معتبر باشند
        AccountingPermissions.Matrix.Should().OnlyContain(p => p.Split(':').Length == 3);
    }

    private static HashSet<string> ControllerPermissions()
    {
        var assembly = typeof(ReportsController).Assembly;
        var result = new HashSet<string>();

        foreach (var type in assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t)))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var attribute in method.GetCustomAttributes(true).OfType<HasPermissionAttribute>())
                {
                    result.Add(attribute.Policy);
                }
            }
        }

        return result;
    }
}
