using ERP.BuildingBlocks.Exceptions;
using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Domain.Exceptions;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// تست‌های یکپارچگی موتور سند حسابداری روی پایگاه‌داده واقعی (SQLite سازگار EF).
/// پوشش سناریوهای اجباری ۱ تا ۹ و ۱۱ تا ۱۵ پرامپت (سناریو ۱۰ هم‌زمانی در VoucherConcurrencyPostgresTests روی PostgreSQL واقعی).
/// </summary>
public class VoucherPostingIntegrationTests : IDisposable
{
    private readonly VoucherTestHarness _harness;
    private readonly FiscalYear _fiscalYear;
    private readonly Account _cashAccount;
    private readonly Account _bankAccount;
    private readonly (DocumentService Docs, VoucherPostingEngine Engine, AccountingDbContext Db) _services;

    public VoucherPostingIntegrationTests()
    {
        _harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        _harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
        _fiscalYear = _harness.SeedOpenFiscalYear(2025);
        _cashAccount = _harness.SeedPostingAccount("1001", "صندوق");
        _bankAccount = _harness.SeedPostingAccount("1002", "بانک ملت");
        _services = _harness.NewServices();
    }

    // ------------------------------------------------------------------
    // سناریو ۱: سند متوازن
    // ------------------------------------------------------------------

    [Fact]
    public async Task BalancedDocument_Post_ShouldPostAtomicallyWithGlEntriesAndAudit()
    {
        var postedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        _harness.Clock.UtcNow = postedAt;
        var performedBy = Guid.NewGuid().ToString();

        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 7_500_000m);

        var result = await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, performedBy));

        result.WasAlreadyPosted.Should().BeFalse();
        result.DocumentNumber.Should().Be(doc.DocumentNumber);
        result.TotalDebit.Should().Be(7_500_000m);

        // وضعیت قطعی و اطلاعات ممیزی کاربر واقعی
        var current = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        current!.Status.Should().Be(DocumentStatus.Posted);
        current.PostedBy.Should().Be(performedBy);
        current.PostedAtUtc.Should().Be(postedAt);

        // سطرهای دفتر کل با ارجاع کامل قابل ردیابی (سند + ردیف + منشأ)
        var glEntries = await _services.Db.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.DocumentId == doc.Id)
            .ToListAsync();
        glEntries.Should().HaveCount(2);
        glEntries.Sum(e => e.DebitAmount).Should().Be(7_500_000m);
        glEntries.Sum(e => e.CreditAmount).Should().Be(7_500_000m);
        glEntries.Should().OnlyContain(e => e.DocumentLineId != Guid.Empty);
        glEntries.Should().OnlyContain(e => e.FiscalPeriodId == doc.FiscalPeriodId);

        // رکورد ممیزی چرخه عمر
        var auditLogs = await _services.Db.DocumentAuditLogs.AsNoTracking()
            .Where(l => l.DocumentId == doc.Id)
            .ToListAsync();
        auditLogs.Select(l => l.Action).Should().Contain([DocumentAuditActions.Created, DocumentAuditActions.Posted]);
        auditLogs.First(l => l.Action == DocumentAuditActions.Posted).PerformedBy.Should().Be(performedBy);
    }

    // ------------------------------------------------------------------
    // سناریو ۲: سند نامتوازن — قطعی نمی‌شود
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnbalancedDocument_Post_ShouldRejectAndLeaveDocumentDraft()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 1_000_000m);

        // ایجاد عدم توازن با ویرایش پیش‌نویس: برداشتن یکی از ردیف‌ها و افزودن ردیف نامتوازن
        var updateCommand = new UpdateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, doc.Id,
            new DateOnly(2025, 1, 15), "سند نامتوازن تست",
            [new VoucherLineCommand(_cashAccount.Id, DebitCredit.Debit, 1_000_000m, "بدهکار"),
             new VoucherLineCommand(_bankAccount.Id, DebitCredit.Credit, 999_999m, "بستانکار")],
            null, Guid.NewGuid().ToString());

        var fresh = _harness.NewServices();
        await fresh.Docs.UpdateDraftAsync(updateCommand);

        var action = async () => await fresh.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        await action.Should().ThrowAsync<UnbalancedDocumentException>()
            .Where(e => e.Difference == 1m); // اختلاف حتی یک واحد کوچک‌تر از Precision

        var current = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        current!.Status.Should().Be(DocumentStatus.Draft, "سند نامتوازن نباید قطعی شود");

        var glCount = await _services.Db.GeneralLedgerEntries.CountAsync(e => e.DocumentId == doc.Id);
        glCount.Should().Be(0, "هیچ سطر دفتر کل بدون قطعی‌سازی کامل ایجاد نشود");
    }

    // ------------------------------------------------------------------
    // سناریو ۳: سند بدون ردیف معتبر
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocumentWithoutValidLines_Post_ShouldReject()
    {
        // سند بدون ردیف با ساخت مستقیم در دامنه ایجاد و ذخیره می‌شود
        var doc = new AccountingDocument(
            _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, _fiscalYear.Periods.First().Id,
            8001, new DateOnly(2025, 1, 15), "سند بدون ردیف");

        using (var seedDb = _harness.NewDbContext())
        {
            seedDb.Documents.Add(doc);
            await seedDb.SaveChangesAsync();
        }

        var action = async () => await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        (
                await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.InsufficientLines");
    }

    // ------------------------------------------------------------------
    // سناریو ۴: سند با ردیف صفر
    // ------------------------------------------------------------------

    [Fact]
    public async Task ZeroAmountLine_ShouldBeRejected()
    {
        var action = async () => await _services.Docs.CreateDraftAsync(new CreateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, _fiscalYear.Id,
            new DateOnly(2025, 1, 15), "سند با ردیف صفر",
            [new VoucherLineCommand(_cashAccount.Id, DebitCredit.Debit, 0m, "ردیف صفر")],
            DocumentType.General, null, null, null, null, null, "user"));

        await action.Should().ThrowAsync<global::ERP.BuildingBlocks.Exceptions.ValidationException>();
    }

    // ------------------------------------------------------------------
    // سناریو ۵: بدهکار و بستانکار هم‌زمان در یک ردیف (داده مخدوش)
    // ------------------------------------------------------------------

    [Fact]
    public async Task CorruptedLineWithBothSidesPositive_Post_ShouldReject()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 2_000_000m);

        // شبیه‌سازی داده مخدوش خارج از مسیر عادی (مثلاً import دستی)
        using (var corruptDb = _harness.NewDbContext())
        {
            var line = await corruptDb.DocumentLines
                .OrderBy(l => l.LineNumber)
                .FirstAsync(l => l.DocumentId == doc.Id); // ردیف اول با قطعیت (سمت بدهکار)
            line.CreditAmount = 2_000_000m; // بدهکار از قبل ۲ میلیون است
            await corruptDb.SaveChangesAsync();
        }

        var fresh = _harness.NewServices();
        var action = async () => await fresh.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        (
                await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.BothSidesPositive");
    }

    // ------------------------------------------------------------------
    // سناریو ۶: سند با حساب غیرفعال
    // ------------------------------------------------------------------

    [Fact]
    public async Task InactiveAccount_Post_ShouldReject()
    {
        var account = _harness.SeedPostingAccount("1003", "حساب غیرفعالشونده");
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, account, _bankAccount, 500_000m);

        // غیرفعال‌سازی حساب پس از ایجاد پیش‌نویس
        using (var adminDb = _harness.NewDbContext())
        {
            var acc = await adminDb.Accounts.FirstAsync(a => a.Id == account.Id);
            acc.Deactivate();
            await adminDb.SaveChangesAsync();
        }

        var action = async () => await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        (
                await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.AccountInactive");
    }

    // ------------------------------------------------------------------
    // سناریو ۷: سند با حساب شرکت دیگر
    // ------------------------------------------------------------------

    [Fact]
    public async Task CrossCompanyAccount_Post_ShouldReject()
    {
        var otherCompanyId = Guid.NewGuid();
        var foreignAccount = _harness.SeedPostingAccount("1004", "حساب شرکت دیگر", companyId: otherCompanyId);

        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, foreignAccount, _bankAccount, 300_000m);

        // زمینه با شرکت فعال خالی: فیلتر شرکت از کار می‌افتد و گارد دفاعی موتور باید بگیرد
        _harness.Act(_harness.TenantId, Guid.Empty);

        var action = async () => await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        await action.Should().ThrowAsync<CrossCompanyAccountException>();
    }

    // ------------------------------------------------------------------
    // سناریو ۸: سند خارج از دوره مالی / سال مالی
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocumentInLockedPeriod_Post_ShouldReject()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 400_000m);

        using (var adminDb = _harness.NewDbContext())
        {
            var period = await adminDb.FiscalPeriods.FirstAsync(p => p.Id == doc.FiscalPeriodId);
            period.Lock();
            await adminDb.SaveChangesAsync();
        }

        var fresh = _harness.NewServices();
        var action = async () => await fresh.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        await action.Should().ThrowAsync<FiscalPeriodClosedException>();
    }

    [Fact]
    public async Task DocumentDateOutsideFiscalYear_Post_ShouldReject()
    {
        var doc = new AccountingDocument(
            _harness.TenantId, _harness.CompanyId, _fiscalYear.Id, _fiscalYear.Periods.Last().Id,
            8002, new DateOnly(2026, 6, 1), "سند با تاریخ خارج از سال مالی ۲۰۲۵");

        doc.AddLine(_cashAccount.Id, DebitCredit.Debit, 100_000m, "بدهکار");
        doc.AddLine(_bankAccount.Id, DebitCredit.Credit, 100_000m, "بستانکار");

        using (var seedDb = _harness.NewDbContext())
        {
            seedDb.Documents.Add(doc);
            await seedDb.SaveChangesAsync();
        }

        var action = async () => await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        (
                await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DocumentDateOutsideFiscalYear");
    }

    [Fact]
    public async Task ClosedFiscalYear_Post_ShouldReject()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 250_000m);

        using (var adminDb = _harness.NewDbContext())
        {
            var fy = await adminDb.FiscalYears.FirstAsync(y => y.Id == _fiscalYear.Id);
            fy.Status = FiscalYearStatus.Closed;
            await adminDb.SaveChangesAsync();
        }

        var fresh = _harness.NewServices();
        var action = async () => await fresh.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        await action.Should().ThrowAsync<FiscalYearNotOpenException>();
    }

    // ------------------------------------------------------------------
    // سناریو ۹: ثبت تکراری (Idempotency)
    // ------------------------------------------------------------------

    [Fact]
    public async Task RepeatedPost_WithSameIdempotencyKey_ShouldReturnExistingResult()
    {
        const string idempotencyKey = "post-req-2025-0001";
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 6_000_000m, idempotencyKey: idempotencyKey);

        var first = await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, idempotencyKey, "user1"));

        var second = await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, idempotencyKey, "user2"));

        first.WasAlreadyPosted.Should().BeFalse();
        second.WasAlreadyPosted.Should().BeTrue();
        second.DocumentId.Should().Be(first.DocumentId);
        second.TotalDebit.Should().Be(first.TotalDebit);

        // سند تنها یک‌بار قطعی شده و سطرهای دفتر کل تکراری ندارند
        var glCount = await _services.Db.GeneralLedgerEntries.CountAsync(e => e.DocumentId == doc.Id);
        glCount.Should().Be(2);
    }

    [Fact]
    public async Task RepeatedPost_WithDifferentIdempotencyKey_ShouldThrowConflict()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 6_000_000m);

        await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, "key-A", "user"));

        var action = async () => await _services.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, "key-B", "user"));

        await action.Should().ThrowAsync<IdempotencyKeyConflictException>();
    }

    // ------------------------------------------------------------------
    // سناریو ۱۱: تغییر سند پس از قطعی‌سازی
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostedDocument_Update_ShouldBeRejected()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 1_500_000m);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        var action = async () => await _services.Docs.UpdateDraftAsync(new UpdateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, doc.Id,
            new DateOnly(2025, 2, 1), "ویرایش غیرمجاز سند قطعی",
            [new VoucherLineCommand(_cashAccount.Id, DebitCredit.Debit, 1_500_000m, "بدهکار")],
            null, "user"));

        (
                await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DocumentNotEditable");

        // سند پس از تلاش ویرایش همچنان قطعی و دست‌نخورده است
        var current = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        current!.Status.Should().Be(DocumentStatus.Posted);
        current.Description.Should().Be("سند پیش‌نویس تست");
    }

    [Fact]
    public async Task SubmittedDocument_Update_ShouldBeRejected()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 1_500_000m);
        await _services.Docs.SubmitForApprovalAsync(
            new SubmitVoucherForApprovalCommand(_harness.TenantId, _harness.CompanyId, doc.Id, "user"));

        var action = async () => await _services.Docs.UpdateDraftAsync(new UpdateVoucherDraftCommand(
            _harness.TenantId, _harness.CompanyId, doc.Id,
            new DateOnly(2025, 2, 1), "ویرایش سند در جریان تأیید",
            [new VoucherLineCommand(_cashAccount.Id, DebitCredit.Debit, 1_500_000m, "بدهکار")],
            null, "user"));

        (
                await action.Should().ThrowAsync<AccountingDomainException>())
            .Which.Code.Should().Be("Accounting.DocumentNotEditable");
    }

    // ------------------------------------------------------------------
    // سناریو ۱۲: حذف سند قطعی
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostedDocument_Delete_ShouldBeRejected()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 1_800_000m);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        var action = async () => await _services.Docs.DeleteDraftAsync(
            new DeleteVoucherDraftCommand(_harness.TenantId, _harness.CompanyId, doc.Id, "user"));

        await action.Should().ThrowAsync<ImmutableDocumentException>();

        // سند قطعی همچنان موجود است (حذف فیزیکی ممنوع)
        var current = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        current.Should().NotBeNull();
        current!.Status.Should().Be(DocumentStatus.Posted);
    }

    // ------------------------------------------------------------------
    // سناریو ۱۳: Reverse سند قطعی
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostedDocument_Reverse_ShouldCreateMirroredPostedReversal()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 3_000_000m);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user1"));

        var fresh = _harness.NewServices();
        var result = await fresh.Engine.ReverseAsync(
            new ReverseVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, new DateOnly(2025, 1, 20), null, null, "user2"));

        result.WasAlreadyReversed.Should().BeFalse();

        var original = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        original!.Status.Should().Be(DocumentStatus.Reversed);
        original.ReversedDocumentId.Should().Be(result.ReversalDocumentId);

        var reversal = await _harness.ReloadDocumentAsync(_services.Db, result.ReversalDocumentId);
        reversal!.Status.Should().Be(DocumentStatus.Posted);
        reversal.TotalDebit.Should().Be(3_000_000m);
        reversal.TotalCredit.Should().Be(3_000_000m);
        reversal.SourceDocumentId.Should().Be(doc.Id, "سند معکوس باید به سند مبدأ ارجاع داشته باشد");
        reversal.PostedBy.Should().Be("user2");

        // سطرهای دفتر کل هر دو سند (اصلی + معکوس)
        var glEntries = await _services.Db.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.DocumentId == doc.Id || e.DocumentId == reversal.Id)
            .ToListAsync();
        glEntries.Should().HaveCount(4);
        glEntries.Count(e => e.DocumentId == reversal.Id).Should().Be(2);
        glEntries.Where(e => e.DocumentId == reversal.Id).Should().OnlyContain(e => e.IsReversal);
    }

    [Fact]
    public async Task AlreadyReversedDocument_SecondReverse_ShouldReturnExistingReversal()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 2_500_000m);
        await _services.Engine.PostAsync(new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        var first = await _harness.NewServices().Engine.ReverseAsync(
            new ReverseVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, new DateOnly(2025, 1, 20), null, null, "user"));

        var second = await _harness.NewServices().Engine.ReverseAsync(
            new ReverseVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, new DateOnly(2025, 1, 21), null, null, "user"));

        second.WasAlreadyReversed.Should().BeTrue();
        second.ReversalDocumentId.Should().Be(first.ReversalDocumentId, "درخواست معکوس‌سازی تکراری سند معکوس موجود را بازمی‌گرداند");
    }

    [Fact]
    public async Task DraftDocument_Reverse_ShouldReject()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 900_000m);

        var action = async () => await _services.Engine.ReverseAsync(
            new ReverseVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, new DateOnly(2025, 1, 20), null, null, "user"));

        await action.Should().ThrowAsync<InvalidDocumentTransitionException>();
    }

    // ------------------------------------------------------------------
    // سناریو ۱۴: Rollback هنگام خطای واقعی Database
    // ------------------------------------------------------------------

    [Fact]
    public async Task DatabaseFailureDuringPost_ShouldRollBackEntireOperation()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 4_000_000m);

        // پیش‌درج یک سطر دفتر کل مخدوش برای همان سند → نقض یکتایی هنگام ذخیره (خطای واقعی پایگاه‌داده)
        var firstLineId = doc.Lines.First().Id;
        using (var corruptDb = _harness.NewDbContext())
        {
            corruptDb.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                TenantId = _harness.TenantId,
                CompanyId = _harness.CompanyId,
                FiscalYearId = _fiscalYear.Id,
                FiscalPeriodId = doc.FiscalPeriodId,
                DocumentId = doc.Id,
                DocumentLineId = firstLineId,
                DocumentNumber = doc.DocumentNumber,
                LineNumber = 1,
                AccountId = _cashAccount.Id,
                EntryDate = doc.DocumentDate,
                Description = "داده مخدوش از قبل",
                Side = DebitCredit.Debit,
                DebitAmount = 4_000_000m,
                CreditAmount = 0m,
                PostedBy = "legacy",
                PostedAtUtc = DateTimeOffset.UtcNow
            });
            await corruptDb.SaveChangesAsync();
        }

        var fresh = _harness.NewServices();
        var action = async () => await fresh.Engine.PostAsync(
            new PostVoucherCommand(_harness.TenantId, _harness.CompanyId, doc.Id, null, "user"));

        await action.Should().ThrowAsync<DbUpdateException>("شکست پایگاه‌داده باید باعث Rollback کامل شود");

        // پس از Rollback: سند پیش‌نویس مانده، سطر دفتر کل جدیدی ثبت نشده، ممیزی قطعی‌سازی وجود ندارد
        var current = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        current!.Status.Should().Be(DocumentStatus.Draft);
        current.PostedBy.Should().BeNull();

        var glCount = await _services.Db.GeneralLedgerEntries.CountAsync(e => e.DocumentId == doc.Id);
        glCount.Should().Be(1, "تنها رکورد مخدوش پیش‌درج‌شده باقی می‌ماند و هیچ سطر جدیدی نوشته نشده است");

        var postedAuditLogs = await _services.Db.DocumentAuditLogs
            .Where(l => l.DocumentId == doc.Id && l.Action == DocumentAuditActions.Posted)
            .ToListAsync();
        postedAuditLogs.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // سناریو ۱۵: عدم امکان دسترسی Tenant دیگر
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocumentOfAnotherTenant_ShouldBeInvisibleAndUnpostable()
    {
        var doc = await _harness.SeedBalancedDraftAsync(_services, _fiscalYear, _cashAccount, _bankAccount, 2_200_000m);

        // مستاجر دیگری روی همان پایگاه‌داده
        var otherTenantId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var otherHarness = VoucherTestHarness.Create(otherTenantId, otherCompanyId, _harness.Connection);
        var otherServices = otherHarness.NewServices();

        // خواندن سند مستاجر دیگر باید ناموفق باشد
        var getAction = async () => await otherServices.Docs.GetDocumentAsync(otherTenantId, otherCompanyId, doc.Id);
        await getAction.Should().ThrowAsync<NotFoundException>();

        // ثبت قطعی سند مستاجر دیگر نیز باید ناموفق باشد
        var postAction = async () => await otherServices.Engine.PostAsync(
            new PostVoucherCommand(otherTenantId, otherCompanyId, doc.Id, null, "attacker"));
        await postAction.Should().ThrowAsync<NotFoundException>();

        // سند مستاجر مبدأ دست‌نخورده است
        var current = await _harness.ReloadDocumentAsync(_services.Db, doc.Id);
        current!.Status.Should().Be(DocumentStatus.Draft);
    }

    // ------------------------------------------------------------------
    // چرخه تأیید بر اساس سیاست شرکت (Two-Step)
    // ------------------------------------------------------------------

    [Fact]
    public async Task TwoStepPolicy_PostFromDraft_ShouldReject_PostFromApproved_ShouldSucceed()
    {
        var twoStepHarness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        twoStepHarness.SeedSettings(DocumentApprovalPolicy.TwoStepApprovalRequired);
        var fy = twoStepHarness.SeedOpenFiscalYear(2025);
        var acc1 = twoStepHarness.SeedPostingAccount("2001", "تنخواه");
        var acc2 = twoStepHarness.SeedPostingAccount("2002", "بانک");
        var services = twoStepHarness.NewServices();

        var doc = await twoStepHarness.SeedBalancedDraftAsync(services, fy, acc1, acc2, 1_000_000m);

        // ثبت مستقیم پیش‌نویس با سیاست دومرحله‌ای ممنوع است
        var directPost = async () => await services.Engine.PostAsync(
            new PostVoucherCommand(twoStepHarness.TenantId, twoStepHarness.CompanyId, doc.Id, null, "user"));
        await directPost.Should().ThrowAsync<InvalidDocumentTransitionException>();

        // مسیر صحیح: Submit → Approve → Post
        await services.Docs.SubmitForApprovalAsync(
            new SubmitVoucherForApprovalCommand(twoStepHarness.TenantId, twoStepHarness.CompanyId, doc.Id, "maker"));
        await services.Docs.ApproveAsync(
            new ApproveVoucherCommand(twoStepHarness.TenantId, twoStepHarness.CompanyId, doc.Id, "checker"));

        var result = await services.Engine.PostAsync(
            new PostVoucherCommand(twoStepHarness.TenantId, twoStepHarness.CompanyId, doc.Id, null, "user"));
        result.WasAlreadyPosted.Should().BeFalse();

        var current = await twoStepHarness.ReloadDocumentAsync(services.Db, doc.Id);
        current!.Status.Should().Be(DocumentStatus.Posted);
        current.ApprovedBy.Should().NotBeNull("اطلاعات تأییدکننده در سند ثبت شده باشد");
    }

    public void Dispose() => _harness.Dispose();
}
