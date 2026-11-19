using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;

namespace ERP.UnitTests;

/// <summary>ساعت ثابت برای تست‌های واحد.</summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>زمینه کاربر بدون هویت برای تست‌های واحد سرویس‌ها.</summary>
public sealed class TestUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; }
    public string? Email => null;
    public bool IsAuthenticated => UserId.HasValue;
    public Guid? ActiveTenantId { get; set; }
    public Guid? ActiveCompanyId { get; set; }
    public Guid? ActiveBranchId { get; set; }
    public IReadOnlyList<string> Roles => [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public bool HasPermission(string permission) => Permissions.Contains(permission);
    public bool IsInRole(string role) => Roles.Contains(role);
}

/// <summary>شناسه همبستگی ثابت برای تست‌های واحد.</summary>
public sealed class FixedCorrelationIdProvider : ICorrelationIdProvider
{
    public string CorrelationId { get; set; } = "unit-test-correlation";
    public string GetCorrelationId() => CorrelationId;
}
