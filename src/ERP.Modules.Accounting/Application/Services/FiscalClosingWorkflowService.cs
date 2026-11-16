using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Accounting.Application.Services;

public interface IFiscalClosingWorkflowService
{
    /// <summary>اجرای گردش‌کار مرحله‌ای اختتامیه: اعتبارسنجی → محاسبه سود/زیان → سند اختتامیه → بستن سال.</summary>
    Task<FiscalClosingResultDto> RunClosingAsync(RunFiscalClosingCommand command, CancellationToken cancellationToken = default);

    /// <summary>وضعیت و سابقه مراحل اختتامیه یک سال مالی.</summary>
    Task<FiscalClosingResultDto?> GetStatusAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);
}

public sealed record RunFiscalClosingCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    Guid RetainedEarningsAccountId,
    string PerformedBy);

/// <summary>
/// گردش‌کار اختتامیه سال مالی — مرحله‌ای و قابل پیگیری:
///   ۱. بررسی وضعیت دوره‌ها (همه دوره‌ها تعریف‌شده و غیر Draft)
///   ۲. بررسی اسناد Draft/Pending (هیچ سند ثبت‌نشده باقی نمانده باشد)
///   ۳. کنترل تراز دفتر کل سال
///   ۴. محاسبه درآمدها، هزینه‌ها و سود/زیان دوره
///   ۵. ایجاد و قطعی‌سازی سند اختتامیه (صفر کردن موقت‌ها و انتقال نتیجه به حساب مقصد)
///   ۶. بستن سال مالی (بسته شدن همه دوره‌ها)
/// شکست در هر مرحله وضعیت Failed و علت را ثبت می‌کند و اجرای مجدد (بازیابی) مجاز است.
/// اجرای مجدد پس از صدور سند اختتامیه، فقط مرحله بستن سال را ادامه می‌دهد (بدون سند تکراری).
/// </summary>
public class FiscalClosingWorkflowService : IFiscalClosingWorkflowService
{
    private readonly AccountingDbContext _db;
    private readonly IDocumentService _documentService;
    private readonly IVoucherPostingEngine _postingEngine;

    public FiscalClosingWorkflowService(
        AccountingDbContext db,
        IDocumentService documentService,
        IVoucherPostingEngine postingEngine)
    {
        _db = db;
        _documentService = documentService;
        _postingEngine = postingEngine;
    }

    public async Task<FiscalClosingResultDto?> GetStatusAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var closing = await LoadAsync(tenantId, companyId, fiscalYearId, cancellationToken);
        return closing is null ? null : await MapToResultAsync(closing, cancellationToken);
    }

    public async Task<FiscalClosingResultDto> RunClosingAsync(RunFiscalClosingCommand command, CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _db.FiscalYears
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.TenantId == command.TenantId
                                      && y.CompanyId == command.CompanyId
                                      && y.Id == command.FiscalYearId, cancellationToken)
            ?? throw new NotFoundException("FiscalYear", command.FiscalYearId);

        if (fiscalYear.Status == FiscalYearStatus.Closed)
        {
            throw new AccountingDomainException("Accounting.ClosingAlreadyDone",
                $"سال مالی '{fiscalYear.Name}' قبلاً بسته شده است؛ اختتامیه تکراری مجاز نیست.");
        }

        if (fiscalYear.Status == FiscalYearStatus.Draft)
        {
            throw new AccountingDomainException("Accounting.ClosingYearNotOpen",
                $"سال مالی '{fiscalYear.Name}' در وضعیت Draft است و قابل اختتامیه نیست.");
        }

        var nowUtc = DateTimeOffset.UtcNow;

        // --- بازیابی یا شروع گردش‌کار ---
        var closing = await LoadAsync(command.TenantId, command.CompanyId, command.FiscalYearId, cancellationToken);

        if (closing is { Status: FiscalClosingStatus.Closed })
        {
            throw new AccountingDomainException("Accounting.ClosingAlreadyDone",
                $"اختتامیه سال مالی '{fiscalYear.Name}' قبلاً با موفقیت کامل شده است.");
        }

        var isResumingWithDocument = false;
        if (closing is not null)
        {
            // بازیابی: اگر سند اختتامیه اجرای قبلی قطعی شده، فقط بستن سال ادامه می‌یابد
            isResumingWithDocument = closing.ClosingDocumentId.HasValue && closing.NetIncome.HasValue;

            closing.Steps.Clear();
            closing.Status = FiscalClosingStatus.Validating;
            closing.FailedReason = null;
            closing.StartedAtUtc = nowUtc;
            closing.StartedBy = command.PerformedBy;
        }
        else
        {
            closing = FiscalYearClosing.Start(
                command.TenantId, command.CompanyId, command.FiscalYearId,
                command.RetainedEarningsAccountId, command.PerformedBy, nowUtc);
            _db.FiscalYearClosings.Add(closing);
        }

        var issues = new List<string>();

        // ---------- مرحله ۱: بررسی وضعیت دوره‌ها ----------
        var step1 = NewStep(_db, closing, 1, "بررسی وضعیت دوره‌ها");
        if (fiscalYear.Periods.Count == 0)
        {
            issues.Add("سال مالی هیچ دوره‌ای ندارد.");
            step1.Fail("سال مالی بدون دوره مالی تعریف‌شده است.");
        }
        else
        {
            var draftPeriods = fiscalYear.Periods.Where(p => p.Status == FiscalPeriodStatus.Draft).ToList();
            if (draftPeriods.Count > 0)
            {
                issues.Add($"{draftPeriods.Count} دوره مالی در وضعیت Draft است: {string.Join(", ", draftPeriods.Select(p => p.Name))}");
                step1.Fail($"{draftPeriods.Count} دوره در وضعیت Draft است.");
            }
            else
            {
                step1.Complete($"{fiscalYear.Periods.Count} دوره بررسی شد؛ هیچ دوره‌ای در وضعیت Draft نیست. " +
                               $"(Open: {fiscalYear.Periods.Count(p => p.Status == FiscalPeriodStatus.Open)}, " +
                               $"Locked: {fiscalYear.Periods.Count(p => p.Status == FiscalPeriodStatus.Locked)}, " +
                               $"Closed: {fiscalYear.Periods.Count(p => p.Status == FiscalPeriodStatus.Closed)})");
            }
        }

        // ---------- مرحله ۲: بررسی اسناد ثبت‌نشده ----------
        var step2 = NewStep(_db, closing, 2, "بررسی اسناد Draft و Pending");
        var unfinalizedStatuses = new[]
        {
            (int)DocumentStatus.Draft, (int)DocumentStatus.PendingApproval,
            (int)DocumentStatus.Reviewed, (int)DocumentStatus.Approved
        };
        var unfinalizedCount = await _db.Documents
            .AsNoTracking()
            .CountAsync(d => d.TenantId == command.TenantId
                             && d.CompanyId == command.CompanyId
                             && d.FiscalYearId == fiscalYear.Id
                             && unfinalizedStatuses.Contains((int)d.Status), cancellationToken);

        if (unfinalizedCount > 0)
        {
            issues.Add($"{unfinalizedCount} سند ثبت‌نشده (Draft/Pending) در سال مالی وجود دارد که باید نهایی یا حذف شود.");
            step2.Fail($"{unfinalizedCount} سند ثبت‌نشده یافت شد.");
        }
        else
        {
            step2.Complete("هیچ سند ثبت‌نشده‌ای باقی نمانده است.");
        }

        // ---------- مرحله ۳: کنترل تراز دفتر کل ----------
        var step3 = NewStep(_db, closing, 3, "کنترل تراز اسناد قطعی");
        var glTotals = await _db.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == command.TenantId
                        && e.CompanyId == command.CompanyId
                        && e.FiscalYearId == fiscalYear.Id)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var glDebit = glTotals?.Debit ?? 0m;
        var glCredit = glTotals?.Credit ?? 0m;
        var glCount = glTotals?.Count ?? 0;

        if (glDebit != glCredit)
        {
            issues.Add($"دفتر کل سال متوازن نیست: جمع بدهکار {glDebit:N4} در برابر بستانکار {glCredit:N4}.");
            step3.Fail($"اختلاف تراز {(glDebit - glCredit):N4}");
        }
        else
        {
            step3.Complete($"دفتر کل متوازن است: {glCount} سطر، جمع بدهکار = بستانکار = {glDebit:N4}");
        }

        // ---------- تصمیم اعتبارسنجی ----------
        closing.MarkValidated(issues, nowUtc);
        await _db.SaveChangesAsync(cancellationToken);

        if (issues.Count > 0)
        {
            return await MapToResultAsync(closing, cancellationToken);
        }

        closing.MarkClosing(nowUtc);

        // ---------- مرحله ۴: محاسبه درآمد، هزینه و سود/زیان ----------
        var step4 = NewStep(_db, closing, 4, "محاسبه درآمدها و هزینه‌ها و سود/زیان دوره");

        var natureTotals = await (
                from entry in _db.GeneralLedgerEntries.AsNoTracking()
                join account in _db.Accounts.AsNoTracking() on entry.AccountId equals account.Id
                where entry.TenantId == command.TenantId
                      && entry.CompanyId == command.CompanyId
                      && entry.FiscalYearId == fiscalYear.Id
                group new { entry.DebitAmount, entry.CreditAmount } by account.Nature into g
                select new { Nature = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(cancellationToken);

        // درآمد: مانده بستانکار (اعتبار مثبت) | هزینه: مانده بدهکار
        var totalRevenue = natureTotals.Where(n => n.Nature == AccountNature.Revenue)
            .Sum(n => n.Credit - n.Debit);
        var totalExpense = natureTotals.Where(n => n.Nature == AccountNature.Expense)
            .Sum(n => n.Debit - n.Credit);
        var netIncome = totalRevenue - totalExpense;

        // اعتبارسنجی حساب مقصد (سود و زیان انباشته)
        var retainedEarnings = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == command.TenantId
                                      && a.CompanyId == command.CompanyId
                                      && a.Id == command.RetainedEarningsAccountId, cancellationToken);

        if (retainedEarnings is null)
        {
            closing.MarkFailed($"حساب مقصد سود و زیان انباشته یافت نشد ({command.RetainedEarningsAccountId}).", nowUtc);
            step4.Fail("حساب مقصد یافت نشد.");
            await _db.SaveChangesAsync(cancellationToken);
            return await MapToResultAsync(closing, cancellationToken);
        }

        if (retainedEarnings.Nature != AccountNature.Equity)
        {
            closing.MarkFailed($"حساب مقصد '{retainedEarnings.FullCode}' باید از nature حقوق صاحبان سهام باشد.", nowUtc);
            step4.Fail($"نature حساب مقصد '{retainedEarnings.Nature}' نامعتبر است.");
            await _db.SaveChangesAsync(cancellationToken);
            return await MapToResultAsync(closing, cancellationToken);
        }

        step4.Complete($"درآمد: {totalRevenue:N4} | هزینه: {totalExpense:N4} | سود (زیان): {netIncome:N4}");
        await _db.SaveChangesAsync(cancellationToken);

        Guid closingDocumentId;
        long closingDocumentNumber;

        if (isResumingWithDocument)
        {
            // ---------- بازیابی: سند اختتامیه اجرای قبلی موجود است ----------
            var stepResume = NewStep(_db, closing, 5, "ادامه با سند اختتامیه اجرای قبلی");
            closingDocumentId = closing.ClosingDocumentId!.Value;
            closingDocumentNumber = closing.ClosingDocumentNumber!.Value;
            stepResume.Complete($"سند اختتامیه {closingDocumentNumber} از اجرای قبلی استفاده شد.");
        }
        else if (totalRevenue == 0m && totalExpense == 0m)
        {
            // سال بدون درآمد/هزینه: سند اختتامیه لازم نیست
            var stepNoop = NewStep(_db, closing, 5, "ایجاد سند اختتامیه");
            closingDocumentId = Guid.Empty;
            closingDocumentNumber = 0;
            stepNoop.Complete("درآمد و هزینه سال صفر است؛ سند اختتامیه لازم نبود.");
        }
        else
        {
            // ---------- مرحله ۵: ایجاد و قطعی‌سازی سند اختتامیه ----------
            var step5 = NewStep(_db, closing, 5, "ایجاد سند اختتامیه");

            try
            {
                // دوره پایان سال باید برای ثبت سند اختتامیه باز باشد
                var closingPeriod = fiscalYear.Periods
                    .FirstOrDefault(p => p.ContainsDate(fiscalYear.EndDate));

                if (closingPeriod is null || closingPeriod.Status != FiscalPeriodStatus.Open)
                {
                    throw new AccountingDomainException("Accounting.ClosingPeriodNotOpen",
                        "دوره پایان سال برای ثبت سند اختتامیه باید باز باشد؛ ابتدا دوره را بازگشایی یا وضعیت آن را اصلاح کنید.");
                }

                var closingLines = new List<VoucherLineCommand>();

                // صفر کردن حساب‌های درآمدی (مانده بستانکار → بدهکار)
                var revenueAccounts = await LoadNonZeroBalancesAsync(
                    command.TenantId, command.CompanyId, fiscalYear.Id, AccountNature.Revenue, cancellationToken);
                foreach (var (accountId, creditBalance) in revenueAccounts)
                {
                    closingLines.Add(new VoucherLineCommand(
                        accountId, DebitCredit.Debit, creditBalance, "اختتامیه — بستن حساب درآمدی"));
                }

                // صفر کردن حساب‌های هزینه‌ای (مانده بدهکار → بستانکار)
                var expenseAccounts = await LoadNonZeroBalancesAsync(
                    command.TenantId, command.CompanyId, fiscalYear.Id, AccountNature.Expense, cancellationToken);
                foreach (var (accountId, debitBalance) in expenseAccounts)
                {
                    closingLines.Add(new VoucherLineCommand(
                        accountId, DebitCredit.Credit, debitBalance, "اختتامیه — بستن حساب هزینه‌ای"));
                }

                // انتقال نتیجه به حساب سود و زیان انباشته
                if (netIncome > 0m)
                {
                    closingLines.Add(new VoucherLineCommand(
                        retainedEarnings.Id, DebitCredit.Credit, netIncome,
                        "اختتامیه — انتقال سود دوره به سود و زیان انباشته"));
                }
                else if (netIncome < 0m)
                {
                    closingLines.Add(new VoucherLineCommand(
                        retainedEarnings.Id, DebitCredit.Debit, Math.Abs(netIncome),
                        "اختتامیه — انتقال زیان دوره به سود و زیان انباشته"));
                }

                var draft = await _documentService.CreateDraftAsync(new CreateVoucherDraftCommand(
                    command.TenantId,
                    command.CompanyId,
                    fiscalYear.Id,
                    fiscalYear.EndDate,
                    $"سند اختتامیه سال مالی {fiscalYear.Name}",
                    closingLines,
                    DocumentType.Closing,
                    BranchId: null,
                    $"Closing-{fiscalYear.YearNumber}",
                    DocumentSourceModules.Accounting,
                    null,
                    $"closing:{command.CompanyId}:{fiscalYear.Id}",
                    command.PerformedBy), cancellationToken);

                var posted = await _postingEngine.PostAsync(new PostVoucherCommand(
                    command.TenantId, command.CompanyId, draft.Id, null, command.PerformedBy), cancellationToken);

                closingDocumentId = draft.Id;
                closingDocumentNumber = posted.DocumentNumber;

                step5.Complete($"سند اختتامیه {posted.DocumentNumber} با {closingLines.Count} ردیف قطعی شد.");
            }
            catch (Exception ex)
            {
                step4.CompleteIfNotAlready();
                step5.Fail(ex.Message);
                closing.MarkFailed($"ایجاد سند اختتامیه ناموفق بود: {ex.Message}", nowUtc);
                await _db.SaveChangesAsync(cancellationToken);
                return await MapToResultAsync(closing, cancellationToken);
            }
        }

        // ---------- مرحله ۶: بستن سال مالی ----------
        var step6 = NewStep(_db, closing, 6, "بستن سال مالی");
        try
        {
            fiscalYear.Close(command.PerformedBy, nowUtc);
            closing.MarkClosed(
                totalRevenue, totalExpense, netIncome,
                closingDocumentId == Guid.Empty ? null : closingDocumentId,
                closingDocumentNumber == 0 ? null : closingDocumentNumber,
                command.PerformedBy, nowUtc);
            step6.Complete($"سال مالی '{fiscalYear.Name}' بسته شد؛ مانده حساب‌های دائمی برای افتتاحیه سال بعد قابل انتقال است.");
        }
        catch (Exception ex)
        {
            step6.Fail(ex.Message);
            closing.MarkFailed($"بستن سال مالی ناموفق بود: {ex.Message}", nowUtc);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await MapToResultAsync(closing, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<FiscalYearClosing?> LoadAsync(Guid tenantId, Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken) =>
        await _db.FiscalYearClosings
            .Include(c => c.Steps)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                      && c.CompanyId == companyId
                                      && c.FiscalYearId == fiscalYearId, cancellationToken);

    /// <summary>
    /// ایجاد و ثبت مرحله از طریق DbSet: مرحله‌های افزودن‌شده صرفاً با navigation
    /// توسط EF به‌اشتباه Modified تشخیص داده می‌شوند (کلید کلاینت‌ساید تنظیم شده).
    /// </summary>
    private static FiscalYearClosingStep NewStep(
        AccountingDbContext db, FiscalYearClosing closing, int stepNumber, string name)
    {
        var step = closing.BeginStep(stepNumber, name);
        db.FiscalYearClosingSteps.Add(step);
        return step;
    }

    /// <summary>مانده غیرصفر حساب‌های یک nature به‌صورت علامت‌دار طبیعی آن nature (بستانکار درآمد / بدهکار هزینه).</summary>
    private async Task<List<(Guid AccountId, decimal Balance)>> LoadNonZeroBalancesAsync(
        Guid tenantId, Guid companyId, Guid fiscalYearId, AccountNature nature, CancellationToken cancellationToken)
    {
        var rows = await (
                from entry in _db.GeneralLedgerEntries.AsNoTracking()
                join account in _db.Accounts.AsNoTracking() on entry.AccountId equals account.Id
                where entry.TenantId == tenantId
                      && entry.CompanyId == companyId
                      && entry.FiscalYearId == fiscalYearId
                      && account.Nature == nature
                group new { entry.DebitAmount, entry.CreditAmount } by entry.AccountId into g
                select new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.AccountId, nature == AccountNature.Revenue ? r.Credit - r.Debit : r.Debit - r.Credit))
            .Where(x => x.Item2 != 0m)
            .OrderBy(x => x.AccountId)
            .ToList();
    }

    private async Task<FiscalClosingResultDto> MapToResultAsync(FiscalYearClosing closing, CancellationToken cancellationToken)
    {
        var fiscalYear = await _db.FiscalYears
            .AsNoTracking()
            .FirstAsync(y => y.Id == closing.FiscalYearId, cancellationToken);

        return new FiscalClosingResultDto(
            closing.Id,
            closing.FiscalYearId,
            fiscalYear.Name,
            closing.Status,
            closing.TotalRevenue,
            closing.TotalExpense,
            closing.NetIncome,
            closing.ClosingDocumentId,
            closing.ClosingDocumentNumber,
            closing.FailedReason,
            closing.StartedAtUtc,
            closing.ClosedAtUtc,
            closing.Steps.OrderBy(s => s.StepNumber)
                .Select(s => new FiscalClosingStepDto(s.StepNumber, s.Name, s.Status, s.Output, s.StartedAtUtc, s.FinishedAtUtc))
                .ToList());
    }
}

/// <summary>افزونه کوچک برای مرحله ۴ در مسیر خطای مرحله ۵.</summary>
internal static class FiscalClosingStepExtensions
{
    public static void CompleteIfNotAlready(this FiscalYearClosingStep step)
    {
        if (step.Status == FiscalClosingStepStatus.Pending)
        {
            step.Complete("محاسبات انجام شد؛ اجرای سند اختتامیه متوقف شد.");
        }
    }
}
