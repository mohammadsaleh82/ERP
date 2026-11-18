using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IOpeningVoucherService
{
    /// <summary>
    /// ایجاد و قطعی‌سازی سند افتتاحیه سال مالی جدید از مانده‌های تأییدشده (قطعی)
    /// سال قبل؛ شامل حساب‌های دائمی، تفصیلی‌ها/اشخاص و مانده انبار از قرارداد Inventory.
    /// Idempotent نیست: ایجاد افتتاحیه تکراری برای همان سال رد می‌شود.
    /// </summary>
    Task<OpeningVoucherResultDto> CreateOpeningVoucherAsync(OpeningVoucherCommand command, CancellationToken cancellationToken = default);

    /// <summary>اطلاعات سند افتتاحیه یک سال مالی (در صورت وجود).</summary>
    Task<OpeningVoucherResultDto?> GetByFiscalYearAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);
}

public sealed record OpeningVoucherCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    string PerformedBy,
    string? Description = null);

public sealed record OpeningVoucherResultDto(
    Guid DocumentId,
    long DocumentNumber,
    Guid FiscalYearId,
    DateOnly DocumentDate,
    int LineCount,
    decimal TotalDebit,
    decimal TotalCredit,
    Guid? PreviousFiscalYearId);

/// <summary>
/// موتور سند افتتاحیه.
/// منبع مانده‌ها: سطرهای دفتر کل قطعی سال قبل (فقط اسناد Posted در دفتر کل وجود دارند).
/// حساب‌های موقت (درآمد/هزینه) انتقال نمی‌شوند — نتیجه آن‌ها با اختتامیه به حساب
/// سود و زیان انباشته منتقل شده و افتتاحیه فقط حساب‌های دائمی را حمل می‌کند.
/// مانده اشخاص/تفصیلی‌ها از انعکاس ابعاد سطرهای دفتر کل استخراج می‌شود و هر گروه
/// به یک ردیف افتتاحیه با همان بعد تبدیل می‌گردد.
/// </summary>
public class OpeningVoucherService : IOpeningVoucherService
{
    /// <summary>انواع بعد مالی حامل مانده (اشخاص و تفصیلی‌ها) در انتقال افتتاحیه.</summary>
    private static readonly FinancialDimensionType[] BalanceCarryingDimensionTypes =
    [
        FinancialDimensionType.DetailAccount,
        FinancialDimensionType.Customer,
        FinancialDimensionType.Supplier,
        FinancialDimensionType.Party,
        FinancialDimensionType.Employee
    ];

    private static readonly AccountNature[] PermanentNatures =
        [AccountNature.Asset, AccountNature.Liability, AccountNature.Equity];

    private readonly AccountingDbContext _db;
    private readonly IDocumentService _documentService;
    private readonly IVoucherPostingEngine _postingEngine;
    private readonly IOpeningInventorySource _inventorySource;

    public OpeningVoucherService(
        AccountingDbContext db,
        IDocumentService documentService,
        IVoucherPostingEngine postingEngine,
        IOpeningInventorySource inventorySource)
    {
        _db = db;
        _documentService = documentService;
        _postingEngine = postingEngine;
        _inventorySource = inventorySource;
    }

    public async Task<OpeningVoucherResultDto> CreateOpeningVoucherAsync(OpeningVoucherCommand command, CancellationToken cancellationToken = default)
    {
        var targetYear = await _db.FiscalYears
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.TenantId == command.TenantId && y.CompanyId == command.CompanyId && y.Id == command.FiscalYearId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", command.FiscalYearId);

        if (targetYear.Status != FiscalYearStatus.Open)
        {
            throw new FiscalYearNotOpenException(targetYear.Name, targetYear.Status);
        }

        // --- جلوگیری از افتتاحیه تکراری ---
        var existingOpening = await _db.Documents
            .AsNoTracking()
            .AnyAsync(d => d.TenantId == command.TenantId
                           && d.CompanyId == command.CompanyId
                           && d.FiscalYearId == targetYear.Id
                           && d.DocumentType == DocumentType.Opening, cancellationToken);

        if (existingOpening)
        {
            throw new AccountingDomainException("Accounting.OpeningAlreadyExists",
                $"برای سال مالی '{targetYear.Name}' قبلاً سند افتتاحیه صادر شده است؛ ایجاد افتتاحیه تکراری مجاز نیست.");
        }

        // --- سال قبل باید بسته باشد (وگرنه مانده‌های موقت هنوز تسویه نشده‌اند) ---
        var previousYear = await _db.FiscalYears
            .AsNoTracking()
            .Where(y => y.TenantId == command.TenantId
                        && y.CompanyId == command.CompanyId
                        && y.StartDate < targetYear.StartDate)
            .OrderByDescending(y => y.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousYear is not null && previousYear.Status != FiscalYearStatus.Closed)
        {
            throw new AccountingDomainException("Accounting.OpeningRequiresClosedPreviousYear",
                $"ایجاد افتتاحیه '{targetYear.Name}' پیش از بسته شدن سال مالی قبلی '{previousYear.Name}' مجاز نیست؛ " +
                "در این حالت مانده افتتاحیه با اختتامیه سال قبل مطابقت نخواهد داشت.");
        }

        // --- استخراج مانده حساب‌های دائمی از دفتر کل قطعی سال قبل ---
        var accountBalances = new Dictionary<Guid, decimal>();
        if (previousYear is not null)
        {
            // حساب‌های قابل ثبت دائمی (معین/تفصیلی) — معیار: AllowPosting
            var permanentAccountIds = await _db.Accounts
                .AsNoTracking()
                .Where(a => a.TenantId == command.TenantId
                            && a.CompanyId == command.CompanyId
                            && PermanentNatures.Contains(a.Nature)
                            && a.AllowPosting)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            var glRows = await _db.GeneralLedgerEntries
                .AsNoTracking()
                .Where(e => e.TenantId == command.TenantId
                            && e.CompanyId == command.CompanyId
                            && e.FiscalYearId == previousYear.Id
                            && permanentAccountIds.Contains(e.AccountId))
                .Select(e => new { e.AccountId, e.DebitAmount, e.CreditAmount })
                .ToListAsync(cancellationToken);

            foreach (var group in glRows.GroupBy(r => r.AccountId))
            {
                var signed = group.Sum(r => r.DebitAmount - r.CreditAmount);
                if (signed != 0m)
                    accountBalances[group.Key] = signed;
            }

            // --- مانده اشخاص/تفصیلی‌ها: هر (حساب، بعد) یک ردیف افتتاحیه ---
            var detailRows = await (
                    from entryDimension in _db.GeneralLedgerEntryDimensions.AsNoTracking()
                    join entry in _db.GeneralLedgerEntries.AsNoTracking()
                        on entryDimension.GeneralLedgerEntryId equals entry.Id
                    where entryDimension.TenantId == command.TenantId
                          && entryDimension.CompanyId == command.CompanyId
                          && entry.FiscalYearId == previousYear.Id
                          && permanentAccountIds.Contains(entry.AccountId)
                          && BalanceCarryingDimensionTypes.Contains(entryDimension.DimensionType)
                    group new { entry.DebitAmount, entry.CreditAmount }
                        by new { entry.AccountId, entryDimension.DimensionType, entryDimension.DimensionId } into g
                    select new
                    {
                        g.Key.AccountId,
                        g.Key.DimensionType,
                        g.Key.DimensionId,
                        Signed = g.Sum(x => x.DebitAmount - x.CreditAmount)
                    })
                .ToListAsync(cancellationToken);

            foreach (var detail in detailRows.Where(d => d.Signed != 0m))
            {
                // مانده گروه تفصیلی از مانده کل حساب کم می‌شود؛ باقیمانده به‌عنوان
                // مانده بدون تفصیلی همان حساب در ردیف جداگانه منتقل می‌شود.
                accountBalances[detail.AccountId] = accountBalances.GetValueOrDefault(detail.AccountId) - detail.Signed;
            }

            var openingLines = new List<VoucherLineCommand>();
            foreach (var detail in detailRows.Where(d => d.Signed != 0m).OrderBy(d => d.AccountId).ThenBy(d => d.DimensionId))
            {
                openingLines.Add(BuildSignedLine(
                    detail.AccountId, detail.Signed,
                    $"افتتاحیه — مانده تفصیلی '{detail.DimensionId}' حساب",
                    [new DimensionAssignmentCommand(detail.DimensionType, detail.DimensionId)]));
            }

            foreach (var (accountId, signed) in accountBalances.Where(b => b.Value != 0m).OrderBy(b => b.Key))
            {
                openingLines.Add(BuildSignedLine(accountId, signed, "افتتاحیه — انتقال مانده حساب", null));
            }

            // --- مانده انبار از قرارداد ماژول Inventory ---
            var inventoryLines = await _inventorySource.GetOpeningLinesAsync(
                command.TenantId, command.CompanyId, targetYear.Id, cancellationToken);

            foreach (var inventoryLine in inventoryLines)
            {
                var inventoryAccount = await _db.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TenantId == command.TenantId
                                              && a.CompanyId == command.CompanyId
                                              && a.Id == inventoryLine.AccountId, cancellationToken)
                    ?? throw new NotFoundException("Account", inventoryLine.AccountId);

                if (!PermanentNatures.Contains(inventoryAccount.Nature))
                {
                    throw new AccountingDomainException("Accounting.OpeningInventoryAccountNotPermanent",
                        $"حساب انبار '{inventoryAccount.FullCode}' برای افتتاحیه باید دائمی باشد.");
                }

                var assignments = inventoryLine.DetailDimensionId.HasValue && inventoryLine.DetailDimensionType.HasValue
                    ? new List<DimensionAssignmentCommand>
                        { new(inventoryLine.DetailDimensionType.Value, inventoryLine.DetailDimensionId.Value) }
                    : null;

                openingLines.Add(BuildSignedLine(
                    inventoryLine.AccountId, inventoryLine.SignedAmount,
                    $"افتتاحیه — مانده انبار: {inventoryLine.Description}", assignments));
            }

            if (openingLines.Count == 0)
            {
                throw new AccountingDomainException("Accounting.OpeningEmpty",
                    "هیچ مانده‌ای برای انتقال به افتتاحیه یافت نشد؛ سند افتتاحیه خالی مجاز نیست.");
            }

            // --- کنترل تراز افتتاحیه پیش از ثبت (پیام خطای اختصاصی) ---
            var imbalance = openingLines.Sum(l => (l.Side == DebitCredit.Debit ? l.Amount : -l.Amount));
            if (imbalance != 0m)
            {
                throw new AccountingDomainException("Accounting.OpeningUnbalanced",
                    $"سند افتتاحیه متوازن نیست؛ اختلاف بدهکار و بستانکار {imbalance:N4} است. " +
                    "مانده‌های منبع یا خطوط قرارداد انبار را بررسی کنید.");
            }

            var draft = await _documentService.CreateDraftAsync(new CreateVoucherDraftCommand(
                command.TenantId,
                command.CompanyId,
                targetYear.Id,
                targetYear.StartDate,
                command.Description ?? $"سند افتتاحیه سال مالی {targetYear.Name}",
                openingLines,
                DocumentType.Opening,
                BranchId: null,
                $"Opening-{targetYear.YearNumber}",
                DocumentSourceModules.Accounting,
                null,
                $"opening:{command.CompanyId}:{targetYear.Id}",
                command.PerformedBy), cancellationToken);

            var posted = await _postingEngine.PostAsync(new PostVoucherCommand(
                command.TenantId, command.CompanyId, draft.Id, null, command.PerformedBy), cancellationToken);

            return new OpeningVoucherResultDto(
                draft.Id,
                posted.DocumentNumber,
                targetYear.Id,
                targetYear.StartDate,
                openingLines.Count,
                posted.TotalDebit,
                posted.TotalCredit,
                previousYear?.Id);
        }

        // سال اول (بدون سال قبل): افتتاحیه فقط از منابع خارجی (مانند انبار) ممکن است
        throw new AccountingDomainException("Accounting.OpeningRequiresPreviousYear",
            $"برای '{targetYear.Name}' سال مالی قبلی یافت نشد؛ افتتاحیه از مانده سال قبل قابل ایجاد نیست.");
    }

    public async Task<OpeningVoucherResultDto?> GetByFiscalYearAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.TenantId == tenantId
                                      && d.CompanyId == companyId
                                      && d.FiscalYearId == fiscalYearId
                                      && d.DocumentType == DocumentType.Opening, cancellationToken);

        if (document is null)
            return null;

        return new OpeningVoucherResultDto(
            document.Id,
            document.DocumentNumber,
            document.FiscalYearId,
            document.DocumentDate,
            document.Lines.Count,
            document.TotalDebit,
            document.TotalCredit,
            null);
    }

    private static VoucherLineCommand BuildSignedLine(Guid accountId, decimal signed, string description, List<DimensionAssignmentCommand>? dimensions)
    {
        // مانده علامت‌دار: بدهکار مثبت، بستانکار منفی
        return signed > 0m
            ? new VoucherLineCommand(accountId, DebitCredit.Debit, signed, description, Dimensions: dimensions ?? [])
            : new VoucherLineCommand(accountId, DebitCredit.Credit, Math.Abs(signed), description, Dimensions: dimensions ?? []);
    }
}
