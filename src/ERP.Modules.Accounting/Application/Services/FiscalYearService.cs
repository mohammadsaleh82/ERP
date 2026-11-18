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

public interface IFiscalYearService
{
    Task<FiscalYearDetailDto> CreateFiscalYearAsync(Guid tenantId, Guid companyId, CreateFiscalYearRequest request, CancellationToken cancellationToken = default);
    Task<List<FiscalYearDto>> GetFiscalYearsAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<FiscalYearDetailDto> GetFiscalYearByIdAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);
    Task<List<FiscalPeriodDto>> GeneratePeriodsAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, GeneratePeriodsRequest request, CancellationToken cancellationToken = default);
    Task<FiscalPeriodDto> CreateCustomPeriodAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CreateCustomPeriodRequest request, CancellationToken cancellationToken = default);
    Task<List<FiscalPeriodDto>> GetPeriodsAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);
    Task OpenPeriodAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, Guid periodId, CancellationToken cancellationToken = default);
    Task ClosePeriodAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, Guid periodId, CancellationToken cancellationToken = default);
    Task OpenFiscalYearAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);
    Task CloseFiscalYearAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, string closedBy, CancellationToken cancellationToken = default);
}

public class FiscalYearService : IFiscalYearService
{
    private readonly AccountingDbContext _dbContext;
    private readonly IFinancialCalendarService _calendarService;
    private readonly IAccountingAuditTrailService _auditTrail;

    public FiscalYearService(AccountingDbContext dbContext, IFinancialCalendarService calendarService, IAccountingAuditTrailService auditTrail)
    {
        _dbContext = dbContext;
        _calendarService = calendarService;
        _auditTrail = auditTrail;
    }

    public async Task<FiscalYearDetailDto> CreateFiscalYearAsync(
        Guid tenantId,
        Guid companyId,
        CreateFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. یکتایی شماره سال مالی در شرکت
        var duplicateYear = await _dbContext.FiscalYears
            .AnyAsync(fy => fy.TenantId == tenantId && fy.CompanyId == companyId && fy.YearNumber == request.YearNumber, cancellationToken);

        if (duplicateYear)
        {
            throw new AccountingDomainException("Accounting.DuplicateFiscalYearNumber",
                $"سال مالی با شماره '{request.YearNumber}' قبلاً در این شرکت تعریف شده است.");
        }

        // 2. محاسبه مرزهای شروع و پایان سال در صورت عدم ارسال دستی
        DateOnly startDate;
        DateOnly endDate;
        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            startDate = request.StartDate.Value;
            endDate = request.EndDate.Value;
        }
        else
        {
            (startDate, endDate) = _calendarService.GetYearBounds(request.CalendarType, request.YearNumber);
        }

        if (endDate <= startDate)
        {
            throw new AccountingDomainException("Accounting.InvalidFiscalYearDates",
                "تاریخ پایان سال مالی باید پس از تاریخ شروع باشد.");
        }

        // 3. بررسی عدم همپوشانی سال‌های مالی در همان شرکت
        var overlappingYear = await _dbContext.FiscalYears
            .FirstOrDefaultAsync(fy =>
                fy.TenantId == tenantId &&
                fy.CompanyId == companyId &&
                startDate <= fy.EndDate && fy.StartDate <= endDate, cancellationToken);

        if (overlappingYear != null)
        {
            throw new AccountingDomainException("Accounting.OverlappingFiscalYears",
                $"بازه زمانی سال مالی جدید ({startDate} تا {endDate}) با سال مالی '{overlappingYear.Name}' ({overlappingYear.StartDate} تا {overlappingYear.EndDate}) همپوشانی دارد.");
        }

        // 4. ایجاد موجودیت سال مالی
        var fiscalYear = new FiscalYear(
            tenantId,
            companyId,
            request.YearNumber,
            request.Name,
            startDate,
            endDate,
            request.CalendarType,
            FiscalYearStatus.Open);

        // 5. تولید خودکار دوره‌های ماهانه یا فصلی در صورت درخواست
        if (request.AutoGeneratePeriods.HasValue)
        {
            var standardPeriods = _calendarService.GenerateStandardPeriods(
                request.CalendarType,
                request.YearNumber,
                request.AutoGeneratePeriods.Value);

            foreach (var sp in standardPeriods)
            {
                fiscalYear.AddPeriod(sp.PeriodNumber, sp.Name, sp.StartDate, sp.EndDate);
            }
        }

        _dbContext.FiscalYears.Add(fiscalYear);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetailDto(fiscalYear);
    }

    public async Task<List<FiscalYearDto>> GetFiscalYearsAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var list = await _dbContext.FiscalYears
            .Include(fy => fy.Periods)
            .Where(fy => fy.TenantId == tenantId && fy.CompanyId == companyId)
            .OrderByDescending(fy => fy.YearNumber)
            .ToListAsync(cancellationToken);

        return list.Select(fy => new FiscalYearDto(
            fy.Id,
            fy.TenantId,
            fy.CompanyId,
            fy.YearNumber,
            fy.Name,
            fy.StartDate,
            fy.EndDate,
            fy.CalendarType,
            fy.Status,
            fy.IsClosed,
            fy.Periods.Count,
            fy.ClosedAtUtc,
            fy.ClosedBy,
            fy.CreatedAtUtc)).ToList();
    }

    public async Task<FiscalYearDetailDto> GetFiscalYearByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _dbContext.FiscalYears
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId && fy.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        return MapToDetailDto(fiscalYear);
    }

    public async Task<List<FiscalPeriodDto>> GeneratePeriodsAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        GeneratePeriodsRequest request,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _dbContext.FiscalYears
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId && fy.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        fiscalYear.EnsureCanModify();

        if (fiscalYear.Periods.Count > 0)
        {
            throw new AccountingDomainException("Accounting.FiscalYearAlreadyHasPeriods",
                $"سال مالی '{fiscalYear.Name}' دارای {fiscalYear.Periods.Count} دوره از پیش تعریف شده است.");
        }

        var standardPeriods = _calendarService.GenerateStandardPeriods(
            fiscalYear.CalendarType,
            fiscalYear.YearNumber,
            request.GenerationType);

        foreach (var sp in standardPeriods)
        {
            fiscalYear.AddPeriod(sp.PeriodNumber, sp.Name, sp.StartDate, sp.EndDate);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return fiscalYear.Periods
            .OrderBy(p => p.PeriodNumber)
            .Select(MapToPeriodDto)
            .ToList();
    }

    public async Task<FiscalPeriodDto> CreateCustomPeriodAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        CreateCustomPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _dbContext.FiscalYears
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId && fy.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        var period = fiscalYear.AddPeriod(
            request.PeriodNumber,
            request.Name,
            request.StartDate,
            request.EndDate,
            request.IsAdjustmentPeriod);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToPeriodDto(period);
    }

    public async Task<List<FiscalPeriodDto>> GetPeriodsAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var periods = await _dbContext.FiscalPeriods
            .Where(p => p.TenantId == tenantId && p.CompanyId == companyId && p.FiscalYearId == fiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync(cancellationToken);

        return periods.Select(MapToPeriodDto).ToList();
    }

    public async Task OpenPeriodAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.FiscalPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.FiscalYearId == fiscalYearId && p.TenantId == tenantId && p.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalPeriod", periodId);

        period.Open();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClosePeriodAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.FiscalPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.FiscalYearId == fiscalYearId && p.TenantId == tenantId && p.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalPeriod", periodId);

        period.Close();
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditTrail.RecordAsync(
            tenantId, companyId, AuditTrailActions.PeriodClosed,
            "FiscalPeriod", period.Id,
            after: new Dictionary<string, object?> { ["periodNumber"] = period.PeriodNumber, ["status"] = period.Status.ToString(), ["endDate"] = period.EndDate.ToString("O") });
    }

    public async Task OpenFiscalYearAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _dbContext.FiscalYears
            .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId && fy.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        fiscalYear.Open();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseFiscalYearAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string closedBy,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _dbContext.FiscalYears
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId && fy.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        fiscalYear.Close(closedBy, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditTrail.RecordAsync(
            tenantId, companyId, AuditTrailActions.FiscalYearClosed,
            "FiscalYear", fiscalYear.Id,
            after: new Dictionary<string, object?> { ["name"] = fiscalYear.Name, ["status"] = fiscalYear.Status.ToString() },
            userId: closedBy);
    }

    private static FiscalYearDetailDto MapToDetailDto(FiscalYear fy) =>
        new(
            fy.Id,
            fy.TenantId,
            fy.CompanyId,
            fy.YearNumber,
            fy.Name,
            fy.StartDate,
            fy.EndDate,
            fy.CalendarType,
            fy.Status,
            fy.IsClosed,
            fy.ClosedAtUtc,
            fy.ClosedBy,
            fy.CreatedAtUtc,
            fy.Periods.OrderBy(p => p.PeriodNumber).Select(MapToPeriodDto).ToList());

    private static FiscalPeriodDto MapToPeriodDto(FiscalPeriod p) =>
        new(
            p.Id,
            p.FiscalYearId,
            p.PeriodNumber,
            p.Name,
            p.StartDate,
            p.EndDate,
            p.Status,
            p.IsAdjustmentPeriod);
}
