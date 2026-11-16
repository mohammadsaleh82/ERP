using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Events;
using ERP.Modules.Accounting.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests;

public class FiscalYearAndPeriodTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void FiscalYear_AddPeriod_WithinBounds_ShouldSucceed()
    {
        var year = new FiscalYear(
            _tenantId,
            _companyId,
            yearNumber: 1403,
            name: "سال مالی ۱۴۰۳",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2025, 3, 20));

        var period1 = year.AddPeriod(
            periodNumber: 1,
            name: "فروردین",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2024, 4, 19));

        year.Periods.Should().ContainSingle();
        period1.PeriodNumber.Should().Be(1);
        period1.FiscalYearId.Should().Be(year.Id);
    }

    [Fact]
    public void FiscalYear_AddPeriod_OutsideBounds_ShouldThrowException()
    {
        var year = new FiscalYear(
            _tenantId,
            _companyId,
            yearNumber: 1403,
            name: "سال مالی ۱۴۰۳",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2025, 3, 20));

        var action = () => year.AddPeriod(
            periodNumber: 1,
            name: "دوره نامعتبر",
            startDate: new DateOnly(2024, 1, 1), // Before year start!
            endDate: new DateOnly(2024, 4, 19));

        action.Should().Throw<AccountingDomainException>()
            .WithMessage("*خارج از محدوده سال مالی*");
    }

    [Fact]
    public void FiscalYear_Close_ShouldCloseAllPeriodsAndEmitEvent()
    {
        var year = new FiscalYear(
            _tenantId,
            _companyId,
            yearNumber: 1403,
            name: "سال مالی ۱۴۰۳",
            startDate: new DateOnly(2024, 3, 20),
            endDate: new DateOnly(2025, 3, 20));

        year.AddPeriod(1, "فروردین", new DateOnly(2024, 3, 20), new DateOnly(2024, 4, 19));
        year.AddPeriod(2, "اردیبهشت", new DateOnly(2024, 4, 20), new DateOnly(2024, 5, 20));

        year.Close("financial_director", DateTimeOffset.UtcNow);

        year.IsClosed.Should().BeTrue();
        year.ClosedBy.Should().Be("financial_director");
        year.Periods.Should().AllSatisfy(p => p.Status.Should().Be(FiscalPeriodStatus.Closed));

        var closedEvent = year.DomainEvents.OfType<FiscalYearClosedEvent>().FirstOrDefault();
        closedEvent.Should().NotBeNull();
        closedEvent!.YearNumber.Should().Be(1403);
    }

    [Fact]
    public void FiscalPeriod_WhenLockedOrClosed_ShouldRejectTransactions()
    {
        var period = new FiscalPeriod(
            _tenantId,
            _companyId,
            fiscalYearId: Guid.NewGuid(),
            periodNumber: 5,
            name: "مرداد",
            startDate: new DateOnly(2024, 7, 22),
            endDate: new DateOnly(2024, 8, 21));

        // Open -> OK
        period.EnsureCanAcceptTransactions();

        // Lock period
        period.Lock();
        var lockAction = () => period.EnsureCanAcceptTransactions();
        lockAction.Should().Throw<FiscalPeriodClosedException>();

        // Close period
        period.Close();
        var closeAction = () => period.EnsureCanAcceptTransactions();
        closeAction.Should().Throw<FiscalPeriodClosedException>();
    }
}

public class TenantAndCompanyOwnershipTests
{
    [Fact]
    public void Entities_WithEmptyTenantOrCompanyId_ShouldThrowValidationException()
    {
        var validId = Guid.NewGuid();
        var emptyId = Guid.Empty;

        // FiscalYear empty tenant
        var actionYearTenant = () => new FiscalYear(emptyId, validId, 1403, "۱۴۰۳", new DateOnly(2024, 3, 20), new DateOnly(2025, 3, 20));
        actionYearTenant.Should().Throw<AccountingDomainException>();

        // FiscalYear empty company
        var actionYearCompany = () => new FiscalYear(validId, emptyId, 1403, "۱۴۰۳", new DateOnly(2024, 3, 20), new DateOnly(2025, 3, 20));
        actionYearCompany.Should().Throw<AccountingDomainException>();

        // AccountingDocument empty tenant
        var actionDocTenant = () => new AccountingDocument(emptyId, validId, validId, validId, 1, new DateOnly(2024, 1, 1), "Test");
        actionDocTenant.Should().Throw<AccountingDomainException>();

        // AccountingDocument empty company
        var actionDocCompany = () => new AccountingDocument(validId, emptyId, validId, validId, 1, new DateOnly(2024, 1, 1), "Test");
        actionDocCompany.Should().Throw<AccountingDomainException>();
    }
}
