using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.Modules.Accounting.Application.Services;

public interface IVoucherPostingEngine
{
    /// <summary>ثبت قطعی اتمیک سند (Posting) با Idempotency و ایجاد سطرهای دفتر کل.</summary>
    Task<PostDocumentResultDto> PostAsync(PostVoucherCommand command, CancellationToken cancellationToken = default);

    /// <summary>معکوس‌سازی اتمیک سند قطعی (Storno) با ایجاد سند معکوس و سطرهای دفتر کل.</summary>
    Task<ReverseDocumentResultDto> ReverseAsync(ReverseVoucherCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// موتور ثبت قطعی اسناد حسابداری.
/// عملیات Posting در یک تراکنش اتمیک انجام می‌شود: قفل سطر سند، اعتبارسنجی مجدد سند،
/// بررسی وضعیت دوره و سال مالی، بررسی حساب‌ها، کنترل تراز، کنترل شماره سند،
/// ثبت وضعیت قطعی، ثبت اطلاعات ممیزی و ایجاد سطرهای دفتر کل.
/// هرگونه شکست در هر مرحله باعث Rollback کامل عملیات می‌شود.
/// </summary>
public class VoucherPostingEngine : IVoucherPostingEngine
{
    private readonly AccountingDbContext _db;
    private readonly IVoucherNumberingService _numberingService;
    private readonly IBalanceSnapshotService _balanceSnapshotService;
    private readonly IDimensionValidationService _dimensionValidation;
    private readonly IClock _clock;

    public VoucherPostingEngine(
        AccountingDbContext db,
        IVoucherNumberingService numberingService,
        IBalanceSnapshotService balanceSnapshotService,
        IDimensionValidationService dimensionValidation,
        IClock clock)
    {
        _db = db;
        _numberingService = numberingService;
        _balanceSnapshotService = balanceSnapshotService;
        _dimensionValidation = dimensionValidation;
        _clock = clock;
    }

    public async Task<PostDocumentResultDto> PostAsync(PostVoucherCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty || command.CompanyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext", "شناسه مستاجر و شرکت برای ثبت قطعی سند الزامی است.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // ۱. قفل انحصاری سطر سند (فقط روی PostgreSQL) برای جلوگیری از قطعی‌سازی هم‌زمان یک سند
            if (_db.Database.IsNpgsql())
            {
                await _db.Documents
                    .FromSqlRaw("SELECT * FROM acc_documents WHERE id = {0} FOR UPDATE", command.DocumentId)
                    .LoadAsync(cancellationToken);
            }

            var doc = await LoadDocumentAsync(command.DocumentId, cancellationToken);

            if (doc.Status == DocumentStatus.Posted)
            {
                EnsureIdempotencyKeyNotConflicting(doc, command.IdempotencyKey);
                await transaction.CommitAsync(cancellationToken);
                return AlreadyPostedResult(doc);
            }

            if (doc.Status == DocumentStatus.Reversed)
                throw new InvalidDocumentTransitionException(doc.Status, DocumentStatus.Posted);

            // ۲. کنترل دسترسی شرکت (جلوگیری از دستکاری DocumentId بین شرکت‌های یک مستاجر)
            if (doc.CompanyId != command.CompanyId)
                throw new NotFoundException("AccountingDocument", command.DocumentId);

            // ۳. ثبت کلید ایدمپوتنسی روی سند پیش از قطعی‌سازی
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
            {
                if (string.IsNullOrWhiteSpace(doc.IdempotencyKey))
                {
                    doc.IdempotencyKey = command.IdempotencyKey.Trim();
                }
                else if (!doc.IdempotencyKey.Equals(command.IdempotencyKey.Trim(), StringComparison.Ordinal))
                {
                    throw new IdempotencyKeyConflictException(command.IdempotencyKey.Trim(), doc.Id);
                }
            }

            var nowUtc = _clock.UtcNow;

            // ۴. اعتبارسنجی مجدد سند: سال مالی و دوره مالی باید باز و محتوی تاریخ سند باشند
            var fiscalYear = await LoadAndValidateFiscalYearAsync(doc, cancellationToken);
            var fiscalPeriod = await LoadAndValidateFiscalPeriodAsync(doc, fiscalYear, cancellationToken);

            // ۵. سیاست تأیید شرکت و کنترل ماشین وضعیت
            var policy = await LoadApprovalPolicyAsync(doc.TenantId, doc.CompanyId, cancellationToken);
            AccountingDocumentWorkflow.EnsureCanPost(doc.Status, policy);

            // ۶. اعتبارسنجی حساب‌های تمام ردیف‌ها (فعال بودن، مجوز ثبت، تعلق به همان شرکت)
            await ValidateDocumentAccountsAsync(doc, cancellationToken);

            // ۷. اعتبارسنجی نهایی ردیف‌ها (عدم وجود بدهکار و بستانکار هم‌زمان در یک ردیف)
            ValidateLines(doc);

            // ۷-الف. اعتبارسنجی مجدد قواعد ابعاد مالی در لحظه قطعی‌سازی (دفاع در عمق؛
            // سیاست‌های حساب و ترکیب‌های ممنوع ممکن است پس از ایجاد پیش‌نویس تغییر کرده باشند)
            await ValidateDocumentDimensionsAsync(doc, cancellationToken);

            // ۸. کنترل تراز بر اساس ارز پایه (اختلاف هرچند کوچک ممنوع)
            if (!doc.IsBalanced)
                throw new UnbalancedDocumentException(doc.TotalDebit, doc.TotalCredit);

            // ۹. ثبت وضعیت قطعی + اطلاعات ممیزی کاربر واقعی
            doc.Post(policy);
            doc.MarkPosted(command.PerformedBy, nowUtc);

            // ۱۰. ایجاد سطرهای دفتر کل با ارجاع کامل به منبع (سند + ردیف) و انعکاس ابعاد مالی
            foreach (var line in doc.Lines.OrderBy(l => l.LineNumber))
            {
                var glEntry = GeneralLedgerEntry.FromDocumentLine(
                    doc, line, fiscalPeriod.Id, command.PerformedBy, nowUtc);
                _db.GeneralLedgerEntries.Add(glEntry);

                foreach (var assignment in line.NormalizeDimensionAssignments())
                {
                    _db.GeneralLedgerEntryDimensions.Add(new GeneralLedgerEntryDimension(
                        doc.TenantId, doc.CompanyId, glEntry.Id, doc.Id, line.Id,
                        assignment.Type, assignment.DimensionId));
                }
            }

            // ۱۱. ثبت رکورد ممیزی چرخه عمر
            _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
                doc.TenantId, doc.CompanyId, doc.Id,
                DocumentAuditActions.Posted, DocumentStatus.Draft, DocumentStatus.Posted,
                command.PerformedBy,
                $"قطعی‌سازی سند {doc.DocumentNumber} با مبلغ {doc.TotalDebit:N4}",
                nowUtc));

            // ۱۲. انبارش گردش‌ها روی اسنپ‌شات مانده حساب‌ها (Materialized Read Model) در همان تراکنش
            await _balanceSnapshotService.ApplyAsync(
                _db, doc.TenantId, doc.CompanyId, doc.FiscalYearId, fiscalPeriod.Id,
                doc.Lines.Select(l => (l.AccountId, l.DebitAmount, l.CreditAmount)).ToList(),
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PostDocumentResultDto(
                doc.Id, doc.DocumentNumber, doc.TotalDebit, doc.TotalCredit,
                nowUtc, command.PerformedBy, WasAlreadyPosted: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            // رقابت هم‌زمان: سند ممکن است توسط درخواست دیگر قطعی شده باشد
            var current = await LoadDocumentNoTrackingAsync(command.DocumentId, CancellationToken.None);
            if (current?.Status == DocumentStatus.Posted)
            {
                EnsureIdempotencyKeyNotConflicting(current, command.IdempotencyKey);
                return AlreadyPostedResult(current);
            }

            throw new AccountingDomainException("Accounting.PostingConcurrencyConflict",
                "درخواست هم‌زمان دیگر وضعیت سند را تغییر داده است. لطفاً مجدداً تلاش فرمایید.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            // نقض یکتایی سطرهای دفتر کل = سند قبلاً توسط درخواست هم‌زمان قطعی شده است
            var current = await LoadDocumentNoTrackingAsync(command.DocumentId, CancellationToken.None);
            if (current?.Status == DocumentStatus.Posted)
            {
                EnsureIdempotencyKeyNotConflicting(current, command.IdempotencyKey);
                return AlreadyPostedResult(current);
            }

            throw;
        }
    }

    public async Task<ReverseDocumentResultDto> ReverseAsync(ReverseVoucherCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty || command.CompanyId == Guid.Empty)
            throw new AccountingDomainException("Accounting.EmptyContext", "شناسه مستاجر و شرکت برای معکوس‌سازی سند الزامی است.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (_db.Database.IsNpgsql())
            {
                await _db.Documents
                    .FromSqlRaw("SELECT * FROM acc_documents WHERE id = {0} FOR UPDATE", command.DocumentId)
                    .LoadAsync(cancellationToken);
            }

            var original = await LoadDocumentAsync(command.DocumentId, cancellationToken);

            if (original.Status == DocumentStatus.Reversed)
            {
                var existingReversal = original.ReversedDocumentId.HasValue
                    ? await LoadDocumentNoTrackingAsync(original.ReversedDocumentId.Value, cancellationToken)
                    : null;

                if (existingReversal is not null)
                {
                    return new ReverseDocumentResultDto(
                        original.Id, existingReversal.Id, existingReversal.DocumentNumber,
                        existingReversal.TotalDebit, WasAlreadyReversed: true);
                }
            }

            // ۱. کنترل دسترسی شرکت
            if (original.CompanyId != command.CompanyId)
                throw new NotFoundException("AccountingDocument", command.DocumentId);

            // ۲. کنترل ماشین وضعیت: تنها اسناد Posted قابل معکوس‌سازی هستند
            AccountingDocumentWorkflow.EnsureCanReverse(original.Status);

            var nowUtc = _clock.UtcNow;

            // ۳. اعتبارسنجی تاریخ معکوس در سال/دوره مالی باز
            var fiscalYear = await _db.FiscalYears
                .Include(y => y.Periods)
                .FirstOrDefaultAsync(y => y.Id == original.FiscalYearId, cancellationToken)
                ?? throw new NotFoundException("FiscalYear", original.FiscalYearId);

            if (fiscalYear.CompanyId != original.CompanyId)
                throw new NotFoundException("AccountingDocument", command.DocumentId);

            if (command.ReversalDate < fiscalYear.StartDate || command.ReversalDate > fiscalYear.EndDate)
                throw new AccountingDomainException("Accounting.DocumentDateOutsideFiscalYear",
                    $"تاریخ سند معکوس '{command.ReversalDate}' خارج از محدوده سال مالی '{fiscalYear.Name}' است.");

            if (fiscalYear.Status != FiscalYearStatus.Open)
                throw new FiscalYearNotOpenException(fiscalYear.Name, fiscalYear.Status);

            var fiscalPeriod = fiscalYear.Periods
                .FirstOrDefault(p => p.ContainsDate(command.ReversalDate))
                ?? throw new DocumentNotInOpenPeriodException(command.ReversalDate);

            fiscalPeriod.EnsureCanAcceptTransactions();

            // ۴. صدور شماره سند معکوس از همان سریال پیوسته سال مالی (شماره سند در کل سال یکتاست)
            var reversalNumber = await _numberingService.NextNumberAsync(
                original.TenantId, original.CompanyId, original.FiscalYearId,
                sequenceType: VoucherSequenceTypes.Voucher,
                periodId: null,
                cancellationToken);

            var reversalDescription = !string.IsNullOrWhiteSpace(command.Description)
                ? command.Description.Trim()
                : $"سند معکوس سند شماره {original.DocumentNumber}";

            // ۵. ایجاد سند معکوس با آینه‌سازی معکوس ردیف‌ها؛ سند مبدأ Reversed می‌شود
            var reversal = original.CreateReversal(reversalNumber, command.ReversalDate, reversalDescription, fiscalPeriod.Id);

            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
                reversal.IdempotencyKey = command.IdempotencyKey.Trim();

            reversal.CurrencyCode = original.CurrencyCode;
            reversal.MarkPosted(command.PerformedBy, nowUtc);

            _db.Documents.Add(reversal);

            // ۶. سطرهای دفتر کل سند معکوس با انعکاس کامل ابعاد مالی
            foreach (var line in reversal.Lines.OrderBy(l => l.LineNumber))
            {
                var glEntry = GeneralLedgerEntry.FromDocumentLine(
                    reversal, line, fiscalPeriod.Id, command.PerformedBy, nowUtc, isReversal: true);
                _db.GeneralLedgerEntries.Add(glEntry);

                foreach (var assignment in line.NormalizeDimensionAssignments())
                {
                    _db.GeneralLedgerEntryDimensions.Add(new GeneralLedgerEntryDimension(
                        reversal.TenantId, reversal.CompanyId, glEntry.Id, reversal.Id, line.Id,
                        assignment.Type, assignment.DimensionId));
                }
            }

            // ۷. رکوردهای ممیزی برای هر دو سند
            _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
                original.TenantId, original.CompanyId, original.Id,
                DocumentAuditActions.Reversed, DocumentStatus.Posted, DocumentStatus.Reversed,
                command.PerformedBy,
                $"معکوس‌سازی سند {original.DocumentNumber} با سند معکوس {reversalNumber}",
                nowUtc));

            _db.DocumentAuditLogs.Add(DocumentAuditLog.Create(
                original.TenantId, original.CompanyId, reversal.Id,
                DocumentAuditActions.Created, null, DocumentStatus.Posted,
                command.PerformedBy,
                $"ایجاد سند معکوس {reversalNumber} برای سند {original.DocumentNumber}",
                nowUtc));

            // انبارش گردش‌های سند معکوس روی اسنپ‌شات‌ها در همان تراکنش
            await _balanceSnapshotService.ApplyAsync(
                _db, reversal.TenantId, reversal.CompanyId, reversal.FiscalYearId, fiscalPeriod.Id,
                reversal.Lines.Select(l => (l.AccountId, l.DebitAmount, l.CreditAmount)).ToList(),
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ReverseDocumentResultDto(
                original.Id, reversal.Id, reversal.DocumentNumber,
                reversal.TotalDebit, WasAlreadyReversed: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new AccountingDomainException("Accounting.ReversalConcurrencyConflict",
                "درخواست هم‌زمان دیگر وضعیت سند را تغییر داده است. لطفاً مجدداً تلاش فرمایید.");
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<AccountingDocument> LoadDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var doc = await _db.Documents
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("AccountingDocument", documentId);

        return doc;
    }

    private async Task<AccountingDocument?> LoadDocumentNoTrackingAsync(Guid documentId, CancellationToken cancellationToken)
    {
        return await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
    }

    private static void EnsureIdempotencyKeyNotConflicting(AccountingDocument doc, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return;

        if (!string.IsNullOrWhiteSpace(doc.IdempotencyKey) &&
            !doc.IdempotencyKey.Equals(idempotencyKey.Trim(), StringComparison.Ordinal))
        {
            throw new IdempotencyKeyConflictException(idempotencyKey.Trim(), doc.Id);
        }
    }

    private async Task<FiscalYear> LoadAndValidateFiscalYearAsync(AccountingDocument doc, CancellationToken cancellationToken)
    {
        var fiscalYear = await _db.FiscalYears
            .FirstOrDefaultAsync(y => y.Id == doc.FiscalYearId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", doc.FiscalYearId);

        if (fiscalYear.CompanyId != doc.CompanyId)
            throw new NotFoundException("AccountingDocument", doc.Id);

        if (fiscalYear.Status != FiscalYearStatus.Open)
            throw new FiscalYearNotOpenException(fiscalYear.Name, fiscalYear.Status);

        if (doc.DocumentDate < fiscalYear.StartDate || doc.DocumentDate > fiscalYear.EndDate)
            throw new AccountingDomainException("Accounting.DocumentDateOutsideFiscalYear",
                $"تاریخ سند '{doc.DocumentDate}' خارج از محدوده سال مالی '{fiscalYear.Name}' ({fiscalYear.StartDate} تا {fiscalYear.EndDate}) است؛ سند دارای تاریخ خارج از سال مالی رد می‌شود.");

        return fiscalYear;
    }

    private async Task<FiscalPeriod> LoadAndValidateFiscalPeriodAsync(AccountingDocument doc, FiscalYear fiscalYear, CancellationToken cancellationToken)
    {
        var period = await _db.FiscalPeriods
            .FirstOrDefaultAsync(p => p.Id == doc.FiscalPeriodId, cancellationToken)
            ?? throw new NotFoundException("FiscalPeriod", doc.FiscalPeriodId);

        if (period.FiscalYearId != fiscalYear.Id)
            throw new AccountingDomainException("Accounting.PeriodYearMismatch",
                "دوره مالی سند با سال مالی سند سازگار نیست.");

        period.EnsureCanAcceptTransactions();

        if (!period.ContainsDate(doc.DocumentDate))
            throw new DocumentNotInOpenPeriodException(doc.DocumentDate);

        return period;
    }

    private async Task<DocumentApprovalPolicy> LoadApprovalPolicyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        var settings = await _db.AccountingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CompanyId == companyId, cancellationToken);

        return settings?.ApprovalPolicy ?? DocumentApprovalPolicy.DirectPostingAllowed;
    }

    private async Task ValidateDocumentAccountsAsync(AccountingDocument doc, CancellationToken cancellationToken)
    {
        var accountIds = doc.Lines.Select(l => l.AccountId).Distinct().ToList();

        var accounts = await _db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var line in doc.Lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
                throw new NotFoundException("Account", line.AccountId);

            // سند با حساب متعلق به شرکت دیگر قطعی نمی‌شود
            if (account.CompanyId != doc.CompanyId)
                throw new CrossCompanyAccountException(account.Id, doc.CompanyId);

            // سند با حساب غیرفعال یا فاقد مجوز ثبت قطعی نمی‌شود
            account.EnsureCanPost();
        }
    }

    private static void ValidateLines(AccountingDocument doc)
    {
        foreach (var line in doc.Lines)
        {
            if (line.DebitAmount > 0m && line.CreditAmount > 0m)
                throw new AccountingDomainException("Accounting.BothSidesPositive",
                    $"ردیف {line.LineNumber} سند '{doc.DocumentNumber}' هم‌زمان مبلغ بدهکار و بستانکار مثبت دارد.");

            if (line.DebitAmount <= 0m && line.CreditAmount <= 0m)
                throw new AccountingDomainException("Accounting.ZeroAmountLine",
                    $"ردیف {line.LineNumber} سند '{doc.DocumentNumber}' بدون مبلغ معتبر است؛ ردیف بدون مبلغ مجاز نیست.");
        }
    }

    private static PostDocumentResultDto AlreadyPostedResult(AccountingDocument doc) =>
        new(
            doc.Id,
            doc.DocumentNumber,
            doc.TotalDebit,
            doc.TotalCredit,
            doc.PostedAtUtc ?? doc.CreatedAtUtc,
            doc.PostedBy ?? "system",
            WasAlreadyPosted: true);

    /// <summary>اعتبارسنجی قواعد ابعاد مالی تمام ردیف‌های سند در تراکنش قطعی‌سازی.</summary>
    private async Task ValidateDocumentDimensionsAsync(AccountingDocument doc, CancellationToken cancellationToken)
    {
        var inputs = doc.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new DocumentLineValidationInput(
                l.AccountId,
                l.LineNumber,
                l.NormalizeDimensionAssignments()))
            .ToList();

        await _dimensionValidation.ValidateDocumentLinesAsync(
            doc.TenantId, doc.CompanyId, inputs, cancellationToken);
    }
}

/// <summary>نوع‌های استاندارد سریال شماره‌گذاری اسناد.</summary>
public static class VoucherSequenceTypes
{
    public const string Voucher = "Voucher";
    public const string Reversal = "Reversal";
}
