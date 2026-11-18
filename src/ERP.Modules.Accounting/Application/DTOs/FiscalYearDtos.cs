using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.DTOs;

public sealed record FiscalYearDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    int YearNumber,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalCalendarType CalendarType,
    FiscalYearStatus Status,
    bool IsClosed,
    int PeriodsCount,
    DateTimeOffset? ClosedAtUtc,
    string? ClosedBy,
    DateTimeOffset CreatedAtUtc);

public sealed record FiscalYearDetailDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    int YearNumber,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalCalendarType CalendarType,
    FiscalYearStatus Status,
    bool IsClosed,
    DateTimeOffset? ClosedAtUtc,
    string? ClosedBy,
    DateTimeOffset CreatedAtUtc,
    List<FiscalPeriodDto> Periods);

public sealed record FiscalPeriodDto(
    Guid Id,
    Guid FiscalYearId,
    int PeriodNumber,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalPeriodStatus Status,
    bool IsAdjustmentPeriod);

public sealed record CreateFiscalYearRequest(
    int YearNumber,
    string Name,
    FiscalCalendarType CalendarType = FiscalCalendarType.Persian,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    PeriodGenerationType? AutoGeneratePeriods = PeriodGenerationType.Monthly);

public sealed record UpdateFiscalYearRequest(
    string Name);

public sealed record GeneratePeriodsRequest(
    PeriodGenerationType GenerationType);

public sealed record CreateCustomPeriodRequest(
    int PeriodNumber,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsAdjustmentPeriod = false);

public sealed record AccountingSettingsDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string DefaultCurrencyCode,
    FiscalCalendarType CalendarType,
    int FiscalYearStartMonth,
    int FiscalYearStartDay,
    int DecimalPlaces,
    RoundingMode RoundingMode,
    VoucherNumberingPolicy VoucherNumberingPolicy,
    AllowNegativeBalancesPolicy AllowNegativeBalancesPolicy,
    DocumentApprovalPolicy ApprovalPolicy,
    Guid? DefaultAccountTemplateId,
    Guid? ActiveFiscalYearId,
    DateOnly? LockDate,
    bool AllowUnbalancedDraft,
    bool AutoNumberingEnabled,
    bool RequireSeparateApprover,
    bool RequireSeparatePoster,
    uint Version,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record UpdateAccountingSettingsRequest(
    string DefaultCurrencyCode,
    FiscalCalendarType CalendarType,
    int FiscalYearStartMonth,
    int FiscalYearStartDay,
    int DecimalPlaces,
    RoundingMode RoundingMode,
    VoucherNumberingPolicy VoucherNumberingPolicy,
    AllowNegativeBalancesPolicy AllowNegativeBalancesPolicy,
    DocumentApprovalPolicy ApprovalPolicy,
    Guid? DefaultAccountTemplateId,
    DateOnly? LockDate,
    bool AllowUnbalancedDraft,
    bool AutoNumberingEnabled,
    bool RequireSeparateApprover = false,
    bool RequireSeparatePoster = false,
    string? ChangeReason = null);

public sealed record AccountingSettingsHistoryDto(
    Guid Id,
    uint Version,
    string DefaultCurrencyCode,
    FiscalCalendarType CalendarType,
    int FiscalYearStartMonth,
    int FiscalYearStartDay,
    int DecimalPlaces,
    RoundingMode RoundingMode,
    VoucherNumberingPolicy VoucherNumberingPolicy,
    AllowNegativeBalancesPolicy AllowNegativeBalancesPolicy,
    DocumentApprovalPolicy ApprovalPolicy,
    DateOnly? LockDate,
    string? ChangedBy,
    DateTimeOffset ChangedAtUtc,
    string? ChangeReason);

public sealed record ConfigureVoucherSequenceRequest(
    string SequenceType,
    string? Prefix,
    string FormatPattern,
    long? StartingValue = null,
    Guid? PeriodId = null);
