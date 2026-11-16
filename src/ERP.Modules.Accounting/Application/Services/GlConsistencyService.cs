using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IGlConsistencyService
{
    /// <summary>
    /// تطبیق‌سنجی اسناد قطعی با گردش‌های دفتر کل:
    /// هیچ سند قطعی بدون گردش و هیچ گردش بدون سند قطعی معتبر نباید وجود داشته باشد.
    /// </summary>
    Task<LedgerConsistencyResultDto> VerifyAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId = null,
        CancellationToken cancellationToken = default);
}

public class GlConsistencyService : IGlConsistencyService
{
    private readonly AccountingDbContext _db;

    public GlConsistencyService(AccountingDbContext db)
    {
        _db = db;
    }

    public async Task<LedgerConsistencyResultDto> VerifyAsync(
        Guid tenantId,
        Guid companyId,
        Guid? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        var documentsQuery = _db.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CompanyId == companyId && d.Status == DocumentStatus.Posted);

        var glQuery = _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

        if (fiscalYearId.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.FiscalYearId == fiscalYearId.Value);
            glQuery = glQuery.Where(e => e.FiscalYearId == fiscalYearId.Value);
        }

        // تجمیع یک‌باره: گردش سند در دفتر کل به تفکیک سند (فقط ستون‌های موردنیاز)
        var glByDocument = await glQuery
            .GroupBy(e => e.DocumentId)
            .Select(g => new
            {
                DocumentId = g.Key,
                Debit = g.Sum(x => x.DebitAmount),
                Credit = g.Sum(x => x.CreditAmount),
                LinesCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var glLookup = glByDocument.ToDictionary(g => g.DocumentId);

        var postedDocs = await documentsQuery
            .Select(d => new { d.Id, d.TotalDebit, d.TotalCredit, LinesCount = d.Lines.Count })
            .ToListAsync(cancellationToken);

        var result = new LedgerConsistencyResultDto
        {
            PostedDocumentsChecked = postedDocs.Count
        };

        foreach (var doc in postedDocs)
        {
            result.PostedDocumentsTotalDebit += doc.TotalDebit;
            result.PostedDocumentsTotalCredit += doc.TotalCredit;

            // سند قطعی بدون گردش دفتر کل یا با گردش ناقص
            if (!glLookup.TryGetValue(doc.Id, out var gl) ||
                gl.LinesCount != doc.LinesCount ||
                gl.Debit != doc.TotalDebit ||
                gl.Credit != doc.TotalCredit)
            {
                result.DocumentsWithMissingGlEntries++;
                result.DocumentsWithMissingGlEntriesIds.Add(doc.Id);
            }
        }

        // گردش دفتر کل بدون سند قطعی معتبر (گردش یتیم)
        var postedDocIds = postedDocs.Select(d => d.Id).ToHashSet();
        foreach (var gl in glByDocument)
        {
            result.GlEntriesTotalDebit += gl.Debit;
            result.GlEntriesTotalCredit += gl.Credit;

            if (!postedDocIds.Contains(gl.DocumentId))
            {
                result.GlEntriesWithoutValidDocument++;
                result.OrphanGlEntryDocumentIds.Add(gl.DocumentId);
            }
        }

        result.IsConsistent = result.DocumentsWithMissingGlEntries == 0
                              && result.GlEntriesWithoutValidDocument == 0;

        return result;
    }
}
