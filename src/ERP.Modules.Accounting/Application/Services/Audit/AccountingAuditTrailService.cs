using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ERP.Modules.Accounting.Application.Services.Audit;

/// <summary>
/// سرویس دفتر ممیزی عمومی — فقط Append و Query؛ هیچ مسیر Update/Delete وجود
/// ندارد. کاربر و شناسه همبستگی از زمینه احراز هویت/درخواست گرفته می‌شود، نه
/// از ورودی کاربر.
/// </summary>
public interface IAccountingAuditTrailService
{
    /// <summary>ثبت یک رخداد حساس با Snapshotهای پاک‌سازی‌شده.</summary>
    Task RecordAsync(
        Guid tenantId, Guid companyId, string action, string entityType, Guid entityId,
        object? before = null, object? after = null, string? reason = null,
        Guid? branchId = null, string? userId = null, CancellationToken cancellationToken = default);

    /// <summary>خواندن دفتر ممیزی با فیلترها — برای ممیزان.</summary>
    Task<PagedResultDto<AuditTrailEntryDto>> QueryAsync(
        Guid tenantId, Guid companyId, AuditTrailQuery query, CancellationToken cancellationToken = default);
}

public sealed record AuditTrailQuery(
    string? Action = null,
    string? EntityType = null,
    Guid? EntityId = null,
    Guid? BranchId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 50);

public sealed record AuditTrailEntryDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    string UserId,
    string Action,
    string EntityType,
    Guid EntityId,
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    string? Reason,
    string? BeforeSnapshot,
    string? AfterSnapshot);

public class AccountingAuditTrailService : IAccountingAuditTrailService
{
    private readonly AccountingDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICorrelationIdProvider _correlationId;

    public AccountingAuditTrailService(
        AccountingDbContext db,
        IClock clock,
        ICurrentUserContext currentUser,
        ICorrelationIdProvider correlationId)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _correlationId = correlationId;
    }

    public async Task RecordAsync(
        Guid tenantId, Guid companyId, string action, string entityType, Guid entityId,
        object? before = null, object? after = null, string? reason = null,
        Guid? branchId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        var entry = AccountingAuditTrail.Create(
            tenantId, companyId,
            userId ?? _currentUser.UserId?.ToString() ?? _currentUser.Email ?? "system",
            action, entityType, entityId,
            _clock.UtcNow,
            _correlationId.GetCorrelationId(),
            reason,
            AuditTrailSanitizer.Serialize(before),
            AuditTrailSanitizer.Serialize(after));

        entry.BranchId = branchId;
        entry.CreatedAtUtc = _clock.UtcNow;

        _db.AccountingAuditTrails.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResultDto<AuditTrailEntryDto>> QueryAsync(
        Guid tenantId, Guid companyId, AuditTrailQuery query, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var baseQuery = _db.AccountingAuditTrails.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Action))
            baseQuery = baseQuery.Where(t => t.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            baseQuery = baseQuery.Where(t => t.EntityType == query.EntityType);
        if (query.EntityId.HasValue)
            baseQuery = baseQuery.Where(t => t.EntityId == query.EntityId.Value);
        if (query.BranchId.HasValue)
            baseQuery = baseQuery.Where(t => t.BranchId == query.BranchId.Value);
        if (query.FromUtc.HasValue)
            baseQuery = baseQuery.Where(t => t.TimestampUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            baseQuery = baseQuery.Where(t => t.TimestampUtc <= query.ToUtc.Value);

        var total = await baseQuery.CountAsync(cancellationToken);

        // مرتب‌سازی زمانی روی PostgreSQL؛ SQLite (تست‌ها) DateTimeOffset را در ORDER BY
        // پشتیبانی نمی‌کند — ترتیب پایدار با شناسه
        var ordered = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? baseQuery.OrderBy(t => t.Id)
            : baseQuery.OrderByDescending(t => t.TimestampUtc).ThenBy(t => t.Id);

        var rows = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AuditTrailEntryDto(
                t.Id, t.TenantId, t.CompanyId, t.BranchId,
                t.UserId, t.Action, t.EntityType, t.EntityId,
                t.TimestampUtc, t.CorrelationId, t.Reason,
                t.BeforeSnapshot, t.AfterSnapshot))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<AuditTrailEntryDto>(rows, total, pageNumber, pageSize);
    }
}
