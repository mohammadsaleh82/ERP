using ERP.Modules.Accounting.Domain.Enums;
using ERP.BuildingBlocks.Events;

namespace ERP.Modules.Accounting.Domain.Events;

public sealed record AccountingDocumentCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid DocumentId,
    Guid TenantId,
    Guid CompanyId,
    long DocumentNumber,
    DateOnly DocumentDate,
    decimal TotalDebit) : IDomainEvent
{
    public static AccountingDocumentCreatedEvent Create(
        Guid documentId,
        Guid tenantId,
        Guid companyId,
        long documentNumber,
        DateOnly documentDate,
        decimal totalDebit) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, documentId, tenantId, companyId, documentNumber, documentDate, totalDebit);
}

public sealed record AccountingDocumentPostedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid DocumentId,
    Guid TenantId,
    Guid CompanyId,
    long DocumentNumber,
    DateOnly DocumentDate,
    decimal TotalAmount) : IDomainEvent
{
    public static AccountingDocumentPostedEvent Create(
        Guid documentId,
        Guid tenantId,
        Guid companyId,
        long documentNumber,
        DateOnly documentDate,
        decimal totalAmount) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, documentId, tenantId, companyId, documentNumber, documentDate, totalAmount);
}

public sealed record AccountingDocumentReversedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid OriginalDocumentId,
    Guid ReversalDocumentId,
    Guid TenantId,
    Guid CompanyId,
    long ReversalDocumentNumber,
    decimal TotalAmount) : IDomainEvent
{
    public static AccountingDocumentReversedEvent Create(
        Guid originalDocumentId,
        Guid reversalDocumentId,
        Guid tenantId,
        Guid companyId,
        long reversalDocumentNumber,
        decimal totalAmount) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, originalDocumentId, reversalDocumentId, tenantId, companyId, reversalDocumentNumber, totalAmount);
}

public sealed record FiscalYearClosedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid FiscalYearId,
    Guid TenantId,
    Guid CompanyId,
    int YearNumber) : IDomainEvent
{
    public static FiscalYearClosedEvent Create(
        Guid fiscalYearId,
        Guid tenantId,
        Guid companyId,
        int yearNumber) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, fiscalYearId, tenantId, companyId, yearNumber);
}

public sealed record AccountingDocumentSubmittedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid DocumentId,
    Guid TenantId,
    Guid CompanyId,
    long DocumentNumber) : IDomainEvent
{
    public static AccountingDocumentSubmittedEvent Create(
        Guid documentId,
        Guid tenantId,
        Guid companyId,
        long documentNumber) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, documentId, tenantId, companyId, documentNumber);
}

public sealed record AccountingDocumentApprovedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid DocumentId,
    Guid TenantId,
    Guid CompanyId,
    long DocumentNumber,
    DocumentStatus Status) : IDomainEvent
{
    public static AccountingDocumentApprovedEvent Create(
        Guid documentId,
        Guid tenantId,
        Guid companyId,
        long documentNumber,
        DocumentStatus status) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, documentId, tenantId, companyId, documentNumber, status);
}

public sealed record AccountingDocumentRejectedEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid DocumentId,
    Guid TenantId,
    Guid CompanyId,
    long DocumentNumber,
    string RejectionReason) : IDomainEvent
{
    public static AccountingDocumentRejectedEvent Create(
        Guid documentId,
        Guid tenantId,
        Guid companyId,
        long documentNumber,
        string rejectionReason) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, documentId, tenantId, companyId, documentNumber, rejectionReason);
}
