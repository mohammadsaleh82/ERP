using ERP.BuildingBlocks.Events;

namespace ERP.Modules.Accounting.Contracts.Events;

public sealed record AccountingVoucherPostedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid CompanyId,
    Guid DocumentId,
    long DocumentNumber,
    DateOnly DocumentDate,
    decimal TotalAmount,
    string? ReferenceNumber,
    string? SourceModule,
    Guid? SourceDocumentId) : IIntegrationEvent;

public sealed record FiscalYearClosedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    int YearNumber,
    string ClosedBy) : IIntegrationEvent;
