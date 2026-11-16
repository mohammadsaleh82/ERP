using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IFiscalYearReopeningService
{
    /// <summary>
    /// بازگشایی رسمی سال مالی بسته‌شده با مجوز ویژه: درخواست‌دهنده و تأییدکننده متمایز،
    /// دلیل الزامی، بررسی اثر بر افتتاحیه سال بعد و ثبت رکورد کامل.
    /// بازگشایی، دوره‌های بسته‌شده را باز نمی‌کند و اسناد قطعی را ویرایش‌پذیر نمی‌سازد.
    /// </summary>
    Task<FiscalYearReopeningDto> ReopenAsync(ReopenFiscalYearCommand command, CancellationToken cancellationToken = default);

    /// <summary>سابقه بازگشایی‌های یک سال مالی.</summary>
    Task<IReadOnlyList<FiscalYearReopeningDto>> GetHistoryAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);
}

public sealed record ReopenFiscalYearCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    string Reason,
    string RequestedBy,
    string ApprovedBy);

/// <summary>سرویس بازگشایی سال مالی بسته‌شده.</summary>
public class FiscalYearReopeningService : IFiscalYearReopeningService
{
    private readonly AccountingDbContext _db;

    public FiscalYearReopeningService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<FiscalYearReopeningDto> ReopenAsync(ReopenFiscalYearCommand command, CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _db.FiscalYears
            .FirstOrDefaultAsync(y => y.TenantId == command.TenantId
                                      && y.CompanyId == command.CompanyId
                                      && y.Id == command.FiscalYearId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", command.FiscalYearId);

        if (fiscalYear.Status != FiscalYearStatus.Closed)
        {
            throw new AccountingDomainException("Accounting.FiscalYearNotClosed",
                $"تنها سال مالی بسته‌شده قابل بازگشایی است؛ وضعیت '{fiscalYear.Name}' اکنون '{fiscalYear.Status}' است.");
        }

        // --- بررسی اثر بر افتتاحیه سال بعد ---
        var nextYear = await _db.FiscalYears
            .AsNoTracking()
            .Where(y => y.TenantId == command.TenantId
                        && y.CompanyId == command.CompanyId
                        && y.StartDate > fiscalYear.EndDate)
            .OrderBy(y => y.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        string nextOpeningImpact;
        if (nextYear is null)
        {
            nextOpeningImpact = "سال مالی بعد تعریف نشده است؛ بازگشایی اثری بر افتتاحیه آینده ندارد.";
        }
        else
        {
            var nextOpeningExists = await _db.Documents
                .AsNoTracking()
                .AnyAsync(d => d.TenantId == command.TenantId
                               && d.CompanyId == command.CompanyId
                               && d.FiscalYearId == nextYear.Id
                               && d.DocumentType == DocumentType.Opening, cancellationToken);

            if (nextOpeningExists)
            {
                // اصلاح در سال بسته پس از صدور افتتاحیه سال بعد، مانده دو سال را ناسازگار می‌کند
                throw new AccountingDomainException("Accounting.ReopeningBlockedByNextOpening",
                    $"برای سال مالی بعد '{nextYear.Name}' سند افتتاحیه صادر شده است؛ بازگشایی '{fiscalYear.Name}' مجاز نیست. " +
                    "اصلاحات را با سند تعدیل/معکوس در اولین سال باز انجام دهید.");
            }

            nextOpeningImpact = $"سال مالی بعد '{nextYear.Name}' وجود دارد اما افتتاحیه آن صادر نشده است؛ " +
                                "پس از اصلاحات، مانده افتتاحیه باید مجدداً استخراج و صادر شود.";
        }

        // رکورد بازگشایی (اعتبارسنجی دلیل/بازیگران/تأییدخود در دامنه)
        var reopening = FiscalYearReopening.Create(
            command.TenantId, command.CompanyId, command.FiscalYearId,
            command.Reason, command.RequestedBy, command.ApprovedBy,
            nextOpeningImpact, DateTimeOffset.UtcNow);

        // بازگشایی سال: دوره‌های بسته بسته می‌مانند؛ اسناد قطعی تغییرناپذیر می‌مانند
        fiscalYear.Reopen(command.ApprovedBy, reopening.ReopenedAtUtc);

        _db.FiscalYearReopenings.Add(reopening);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(reopening);
    }

    public async Task<IReadOnlyList<FiscalYearReopeningDto>> GetHistoryAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        // مرتب‌سازی سمت کلاینت: SQLite از ORDER BY روی DateTimeOffset پشتیبانی نمی‌کند
        var rows = await _db.FiscalYearReopenings
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId && r.FiscalYearId == fiscalYearId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.ReopenedAtUtc)
            .Select(ToDto)
            .ToList();
    }

    private static FiscalYearReopeningDto ToDto(FiscalYearReopening reopening) =>
        new(reopening.Id, reopening.FiscalYearId, reopening.Reason, reopening.RequestedBy,
            reopening.ApprovedBy, reopening.ReopenedAtUtc, reopening.NextOpeningImpact, reopening.ChangesSummary);
}
