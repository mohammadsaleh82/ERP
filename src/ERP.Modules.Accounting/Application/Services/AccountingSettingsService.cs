using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services.Audit;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IAccountingSettingsService
{
    Task<AccountingSettingsDto> GetSettingsAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<AccountingSettingsDto> UpdateSettingsAsync(Guid tenantId, Guid companyId, UpdateAccountingSettingsRequest request, string? updatedBy = null, CancellationToken cancellationToken = default);
    Task<List<AccountingSettingsHistoryDto>> GetSettingsHistoryAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}

public class AccountingSettingsService : IAccountingSettingsService
{
    private readonly AccountingDbContext _dbContext;
    private readonly Audit.IAccountingAuditTrailService _auditTrail;

    public AccountingSettingsService(AccountingDbContext dbContext, Audit.IAccountingAuditTrailService auditTrail)
    {
        _dbContext = dbContext;
        _auditTrail = auditTrail;
    }

    public async Task<AccountingSettingsDto> GetSettingsAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.AccountingSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CompanyId == companyId, cancellationToken);

        if (settings is null)
        {
            settings = new AccountingSettings(tenantId, companyId);
            _dbContext.AccountingSettings.Add(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(settings);
    }

    public async Task<AccountingSettingsDto> UpdateSettingsAsync(
        Guid tenantId,
        Guid companyId,
        UpdateAccountingSettingsRequest request,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.AccountingSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CompanyId == companyId, cancellationToken);

        if (settings is null)
        {
            settings = new AccountingSettings(tenantId, companyId);
            _dbContext.AccountingSettings.Add(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Snapshot وضعیت پیش برای ممیزی
        var beforeSnapshot = new Dictionary<string, object?>
        {
            ["approvalPolicy"] = settings.ApprovalPolicy.ToString(),
            ["requireSeparateApprover"] = settings.RequireSeparateApprover,
            ["requireSeparatePoster"] = settings.RequireSeparatePoster,
            ["allowUnbalancedDraft"] = settings.AllowUnbalancedDraft,
            ["autoNumberingEnabled"] = settings.AutoNumberingEnabled,
            ["lockDate"] = settings.LockDate?.ToString("O"),
            ["version"] = (long)settings.Version
        };

        // 1. ثبت سابقه تغییرات در تاریخچه پیش از اعمال ویرایش جدید
        var historySnapshot = AccountingSettingsHistory.CreateSnapshot(
            settings,
            updatedBy,
            DateTimeOffset.UtcNow,
            request.ChangeReason);

        _dbContext.SettingsHistory.Add(historySnapshot);

        // 2. اعمال تغییرات بر روی تنظیمات فعال
        settings.Update(
            request.DefaultCurrencyCode,
            request.CalendarType,
            request.FiscalYearStartMonth,
            request.FiscalYearStartDay,
            request.DecimalPlaces,
            request.RoundingMode,
            request.VoucherNumberingPolicy,
            request.AllowNegativeBalancesPolicy,
            request.ApprovalPolicy,
            request.DefaultAccountTemplateId,
            request.LockDate,
            request.AllowUnbalancedDraft,
            request.AutoNumberingEnabled,
            request.RequireSeparateApprover,
            request.RequireSeparatePoster);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // ثبت ممیزی تغییر تنظیمات با Snapshot پیش/پس (پاک‌سازی‌شده)
        await _auditTrail.RecordAsync(
            tenantId, companyId, AuditTrailActions.SettingsUpdated, "AccountingSettings", settings.Id,
            before: beforeSnapshot,
            after: new Dictionary<string, object?>
            {
                ["approvalPolicy"] = settings.ApprovalPolicy.ToString(),
                ["requireSeparateApprover"] = settings.RequireSeparateApprover,
                ["requireSeparatePoster"] = settings.RequireSeparatePoster,
                ["allowUnbalancedDraft"] = settings.AllowUnbalancedDraft,
                ["autoNumberingEnabled"] = settings.AutoNumberingEnabled,
                ["lockDate"] = settings.LockDate?.ToString("O"),
                ["version"] = (long)settings.Version
            },
            reason: request.ChangeReason,
            cancellationToken: cancellationToken);

        return MapToDto(settings);
    }

    public async Task<List<AccountingSettingsHistoryDto>> GetSettingsHistoryAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var history = await _dbContext.SettingsHistory
            .Where(h => h.TenantId == tenantId && h.CompanyId == companyId)
            .OrderByDescending(h => h.Version)
            .ToListAsync(cancellationToken);

        return history.Select(h => new AccountingSettingsHistoryDto(
            h.Id,
            h.Version,
            h.DefaultCurrencyCode,
            h.CalendarType,
            h.FiscalYearStartMonth,
            h.FiscalYearStartDay,
            h.DecimalPlaces,
            h.RoundingMode,
            h.VoucherNumberingPolicy,
            h.AllowNegativeBalancesPolicy,
            h.ApprovalPolicy,
            h.LockDate,
            h.ChangedBy,
            h.ChangedAtUtc,
            h.ChangeReason)).ToList();
    }

    private static AccountingSettingsDto MapToDto(AccountingSettings s) =>
        new(
            s.Id,
            s.TenantId,
            s.CompanyId,
            s.DefaultCurrencyCode,
            s.CalendarType,
            s.FiscalYearStartMonth,
            s.FiscalYearStartDay,
            s.DecimalPlaces,
            s.RoundingMode,
            s.VoucherNumberingPolicy,
            s.AllowNegativeBalancesPolicy,
            s.ApprovalPolicy,
            s.DefaultAccountTemplateId,
            s.ActiveFiscalYearId,
            s.LockDate,
            s.AllowUnbalancedDraft,
            s.AutoNumberingEnabled,
            s.RequireSeparateApprover,
            s.RequireSeparatePoster,
            s.Version,
            s.LastModifiedAtUtc);
}
