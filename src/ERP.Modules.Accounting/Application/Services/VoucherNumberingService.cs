using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IVoucherNumberingService
{
    Task<long> NextNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType = "Posted",
        Guid? periodId = null,
        CancellationToken cancellationToken = default);

    Task<string> NextFormattedNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType = "Posted",
        Guid? periodId = null,
        CancellationToken cancellationToken = default);

    Task ConfigureSequenceAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType,
        string? prefix,
        string formatPattern,
        long? startingValue = null,
        Guid? periodId = null,
        CancellationToken cancellationToken = default);
}

public class VoucherNumberingService : IVoucherNumberingService
{
    private readonly AccountingDbContext _dbContext;

    public VoucherNumberingService(AccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> NextNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType = "Posted",
        Guid? periodId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyTenantId", "TenantId الزامی است.");
        if (companyId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyCompanyId", "CompanyId الزامی است.");
        if (fiscalYearId == Guid.Empty) throw new AccountingDomainException("Accounting.EmptyFiscalYearId", "FiscalYearId الزامی است.");

        const int maxRetries = 10;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var sequence = await _dbContext.VoucherSequences
                    .FirstOrDefaultAsync(s =>
                        s.TenantId == tenantId &&
                        s.CompanyId == companyId &&
                        s.FiscalYearId == fiscalYearId &&
                        s.SequenceType == sequenceType &&
                        s.PeriodId == periodId, cancellationToken);

                long nextVal;
                if (sequence is null)
                {
                    sequence = new VoucherSequence(tenantId, companyId, fiscalYearId, sequenceType, periodId);
                    nextVal = sequence.NextValue();
                    _dbContext.VoucherSequences.Add(sequence);
                }
                else
                {
                    nextVal = sequence.NextValue();
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return nextVal;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt == maxRetries - 1)
                    throw new AccountingDomainException("Accounting.ConcurrencyCollision", "خطای همزمانی در صدور شماره سند حسابداری. لطفاً مجدداً تلاش فرمایید.");

                await Task.Delay(Random.Shared.Next(15, 60), cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true || ex.InnerException?.Message.Contains("duplicate") == true)
            {
                if (attempt == maxRetries - 1)
                    throw new AccountingDomainException("Accounting.SequenceGenerationCollision", "تداخل در تولید شماره سریال سند رخ داده است.");

                await Task.Delay(Random.Shared.Next(15, 60), cancellationToken);
            }
        }

        throw new AccountingDomainException("Accounting.SequenceGenerationFailed", "تولید شماره سند با شکست مواجه شد.");
    }

    public async Task<string> NextFormattedNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType = "Posted",
        Guid? periodId = null,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _dbContext.FiscalYears
            .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId && fy.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", fiscalYearId);

        var nextSeq = await NextNumberAsync(tenantId, companyId, fiscalYearId, sequenceType, periodId, cancellationToken);

        var sequence = await _dbContext.VoucherSequences
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.CompanyId == companyId &&
                s.FiscalYearId == fiscalYearId &&
                s.SequenceType == sequenceType &&
                s.PeriodId == periodId, cancellationToken);

        var prefix = sequence?.Prefix ?? string.Empty;
        var pattern = sequence?.FormatPattern ?? "{Seq}";

        return FormatNumber(pattern, prefix, fiscalYear.YearNumber, nextSeq);
    }

    public async Task ConfigureSequenceAsync(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType,
        string? prefix,
        string formatPattern,
        long? startingValue = null,
        Guid? periodId = null,
        CancellationToken cancellationToken = default)
    {
        var sequence = await _dbContext.VoucherSequences
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.CompanyId == companyId &&
                s.FiscalYearId == fiscalYearId &&
                s.SequenceType == sequenceType &&
                s.PeriodId == periodId, cancellationToken);

        if (sequence is null)
        {
            sequence = new VoucherSequence(tenantId, companyId, fiscalYearId, sequenceType, periodId, prefix, formatPattern);
            if (startingValue.HasValue)
            {
                sequence.CurrentValue = startingValue.Value;
            }
            _dbContext.VoucherSequences.Add(sequence);
        }
        else
        {
            sequence.Prefix = prefix;
            sequence.FormatPattern = string.IsNullOrWhiteSpace(formatPattern) ? "{Seq}" : formatPattern.Trim();
            if (startingValue.HasValue)
            {
                if (startingValue.Value < sequence.CurrentValue)
                {
                    throw new AccountingDomainException("Accounting.CannotReduceSequence",
                        $"امکان کاهش شماره سریال فعلی ({sequence.CurrentValue}) به ({startingValue.Value}) وجود ندارد.");
                }
                sequence.CurrentValue = startingValue.Value;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string FormatNumber(string pattern, string prefix, int yearNumber, long seq)
    {
        var result = pattern
            .Replace("{Prefix}", prefix, StringComparison.OrdinalIgnoreCase)
            .Replace("{YearNumber}", yearNumber.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{Year}", yearNumber.ToString(), StringComparison.OrdinalIgnoreCase);

        if (result.Contains("{Seq:D6}", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("{Seq:D6}", seq.ToString("D6"), StringComparison.OrdinalIgnoreCase);
        }
        else if (result.Contains("{Seq:D5}", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("{Seq:D5}", seq.ToString("D5"), StringComparison.OrdinalIgnoreCase);
        }
        else if (result.Contains("{Seq:D4}", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("{Seq:D4}", seq.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            result = result.Replace("{Seq}", seq.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
