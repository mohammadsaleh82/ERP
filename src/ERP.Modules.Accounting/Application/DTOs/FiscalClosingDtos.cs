using ERP.Modules.Accounting.Domain.Enums;

namespace ERP.Modules.Accounting.Application.DTOs;

public sealed record FiscalClosingStepDto(
    int StepNumber,
    string Name,
    FiscalClosingStepStatus Status,
    string? Output,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

public sealed record FiscalClosingResultDto(
    Guid Id,
    Guid FiscalYearId,
    string FiscalYearName,
    FiscalClosingStatus Status,
    decimal? TotalRevenue,
    decimal? TotalExpense,
    decimal? NetIncome,
    Guid? ClosingDocumentId,
    long? ClosingDocumentNumber,
    string? FailedReason,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<FiscalClosingStepDto> Steps);

public sealed record FiscalYearReopeningDto(
    Guid Id,
    Guid FiscalYearId,
    string Reason,
    string RequestedBy,
    string ApprovedBy,
    DateTimeOffset ReopenedAtUtc,
    string NextOpeningImpact,
    string? ChangesSummary);
