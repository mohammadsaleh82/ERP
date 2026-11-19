using ERP.BuildingBlocks.Persistence;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.BuildingBlocks.Tenancy;
using System.Text.Json;

namespace ERP.Modules.Accounting.Domain.Entities;

/// <summary>
/// دفتر ممیزی عمومی ماژول حسابداری (Append-Only) — ثبت همه عملیات حساس با
/// کاربر، زمینه، شناسه همبستگی و Before/After پاک‌سازی‌شده. رکوردهای این دفتر
/// از طریق API هیچ مسیر تغییر یا حذفی ندارند؛ در سطح PostgreSQL هم با
/// Trigger/Role محدود می‌شوند (scripts/db/postgres-security.sql).
/// </summary>
public class AccountingAuditTrail : Entity, ITenantScoped, ICompanyScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    /// <summary>کاربر انجام‌دهنده عملیات (از توکن احراز هویت؛ نه از ورودی کاربر).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>کد عملیات (Voucher.Posted، Account.Deactivated، Settings.Updated و ...).</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>شناسه همبستگی درخواست (هدر X-Correlation-Id) برای ردیابی زنجیره عملیات.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>دلیل عملیات (علت رد، دلیل معکوس‌سازی و ...) — بدون داده حساس.</summary>
    public string? Reason { get; set; }

    /// <summary>Snapshot وضعیت پیش — پاک‌سازی‌شده (بدون رمز/توکن)، حداکثر ۸۰۰۰ کاراکتر.</summary>
    public string? BeforeSnapshot { get; set; }

    /// <summary>Snapshot وضعیت پس — پاک‌سازی‌شده.</summary>
    public string? AfterSnapshot { get; set; }

    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    private AccountingAuditTrail()
    {
    }

    public static AccountingAuditTrail Create(
        Guid tenantId, Guid companyId, string userId, string action,
        string entityType, Guid entityId, DateTimeOffset timestampUtc,
        string correlationId, string? reason = null, string? beforeSnapshot = null, string? afterSnapshot = null)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.AuditRequiresContext",
                "ثبت ممیزی بدون زمینه مستاجر و شرکت مجاز نیست.");
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(entityType))
            throw new AccountingDomainException("Accounting.AuditActionRequired",
                "نوع عملیات و موجودیت برای ثبت ممیزی الزامی است.");

        return new AccountingAuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            UserId = userId ?? string.Empty,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            TimestampUtc = timestampUtc,
            CorrelationId = correlationId ?? string.Empty,
            Reason = AuditTrailSanitizer.SanitizeText(reason),
            BeforeSnapshot = Serialize(beforeSnapshot),
            AfterSnapshot = Serialize(afterSnapshot),
            Version = 1
        };
    }

    private static string? Serialize(object? value) => AuditTrailSanitizer.Serialize(value);
}

/// <summary>
/// پاک‌سازی Snapshotهای ممیزی: کلیدهای حساس (رمز، توکن، کلید) ماسک می‌شوند و
/// طول هر Snapshot سقف می‌خورد تا از تزریق داده حساس یا انفجار حجم جلوگیری شود.
/// </summary>
public static class AuditTrailSanitizer
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "passwd", "pwd", "token", "secret", "apikey", "api_key",
        "authorization", "credential", "idempotencykey", "connectionstring"
    ];

    private const int MaxSnapshotLength = 8000;

    public static string? Serialize(object? value)
    {
        if (value is null)
            return null;

        var json = JsonSerializer.Serialize(DeepRedact(value), SanitizerOptions);
        return json.Length > MaxSnapshotLength ? json[..MaxSnapshotLength] : json;
    }

    public static string? SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static object? DeepRedact(object? value) =>
        value switch
        {
            null => null,
            Dictionary<string, object?> dict => dict.ToDictionary(
                kv => kv.Key,
                kv => IsSensitive(kv.Key) ? (object?)"[REDACTED]" : DeepRedact(kv.Value)),
            Dictionary<string, string> dict => dict.ToDictionary(
                kv => kv.Key,
                kv => IsSensitive(kv.Key) ? "[REDACTED]" : kv.Value),
            IEnumerable<KeyValuePair<string, object?>> pairs => pairs.ToDictionary(
                kv => kv.Key,
                kv => IsSensitive(kv.Key) ? (object?)"[REDACTED]" : DeepRedact(kv.Value)),
            string s => s.Length > 500 ? s[..500] : s,
            decimal or int or long or bool or DateTime or DateTimeOffset or DateOnly or Guid => value,
            _ => value.ToString() is { } str && str.Length > 200 ? str[..200] : value
        };

    private static bool IsSensitive(string key) =>
        SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions SanitizerOptions = new(JsonSerializerDefaults.Web);
}
