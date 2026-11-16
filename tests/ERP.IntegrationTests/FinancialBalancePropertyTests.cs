using ERP.Modules.Accounting.Application.Commands;
using ERP.Modules.Accounting.Contracts.Interfaces;
using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Entities;
using ERP.Modules.Accounting.Domain.Enums;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// تست‌های Property-Based حفظ تراز مالی در چرخه سال مالی (PROMPT 07).
/// برای هر seed: دیتاست تصادفیِ «متوازن» از اسناد قطعی تولید و کل چرخه
/// افتتاحیه → عملیات → اختتامیه → افتتاحیه سال بعد اجرا می‌شود؛ سپس ناورداها:
///   I1. جمع بدهکار = جمع بستانکار در هر مقطع (دفتر کل سال)
///   I2. پس از اختتامیه، حساب‌های موقت دقیقاً صفرند
///   I3. سود/زیان محاسبه‌شده = جمع مستقل از GL و = مانده حساب مقصد
///   I4. حساب‌های دائمی در افتتاحیه سال بعد با همان مانده ادامه می‌یابند
///   I5. اختتامیه مانده حساب‌های دائمی را تغییر نمی‌دهد
/// </summary>
public class FinancialBalancePropertyTests
{
    private const int SeedCount = 20;

    [Fact]
    public async Task BalanceIsPreservedAcrossFullFiscalCycle_ForRandomBalancedDatasets()
    {
        for (var seed = 1; seed <= SeedCount; seed++)
        {
            await RunOneCycle(seed);
        }
    }

    private async Task RunOneCycle(int seed)
    {
        var rng = new Random(seed);
        var harness = VoucherTestHarness.Create(Guid.NewGuid(), Guid.NewGuid());
        try
        {
            harness.SeedSettings(DocumentApprovalPolicy.DirectPostingAllowed);
            var year1 = harness.SeedOpenFiscalYear(2025);
            var year2 = harness.SeedOpenFiscalYear(2026);

            var cash = harness.SeedPostingAccount("1001", "صندوق");
            var bank = harness.SeedPostingAccount("1002", "بانک");
            var payable = harness.SeedPostingAccount("2101", "پرداختنی", nature: AccountNature.Liability);
            var capital = harness.SeedPostingAccount("3101", "سرمایه", nature: AccountNature.Equity);
            var retained = harness.SeedPostingAccount("3102", "سود انباشته", nature: AccountNature.Equity);
            var revenue = harness.SeedPostingAccount("4101", "درآمد", nature: AccountNature.Revenue);
            var expense = harness.SeedPostingAccount("5101", "هزینه", nature: AccountNature.Expense);

            var permanent = new[] { cash, bank, payable, capital, retained };
            var temporary = new[] { revenue, expense };

            var (docs, engine, db) = harness.NewServices();

            // --- تولید تصادفی اسناد متوازن (۳ تا ۸ سند) ---
            var documentCount = 3 + rng.Next(6);
            for (var i = 0; i < documentCount; i++)
            {
                var month = 1 + rng.Next(12);
                var day = 1 + rng.Next(27);
                var date = new DateOnly(2025, month, day);
                var amount = (1 + rng.Next(4000)) * 0.25m;

                // نیمی از اسناد: دائمی↔دائمی (عملیات سرمایه/تسویه)، بقیه: عملیات موقت
                var useTemporary = rng.Next(2) == 1;
                Guid debit, credit;
                if (useTemporary)
                {
                    // فروش یا هزینه با طرف دائمی
                    if (rng.Next(2) == 1)
                    {
                        debit = permanent[rng.Next(permanent.Length)].Id;
                        credit = revenue.Id;
                    }
                    else
                    {
                        debit = expense.Id;
                        credit = permanent[rng.Next(permanent.Length)].Id;
                    }
                }
                else
                {
                    debit = permanent[rng.Next(permanent.Length)].Id;
                    credit = permanent[rng.Next(permanent.Length)].Id;
                    if (debit == credit)
                        debit = cash.Id == credit ? bank.Id : cash.Id;
                }

                var draft = await docs.CreateDraftAsync(new CreateVoucherDraftCommand(
                    harness.TenantId, harness.CompanyId, year1.Id, date, $"سند تصادفی {i + 1}",
                    [
                        new VoucherLineCommand(debit, DebitCredit.Debit, amount, "طرف بدهکار"),
                        new VoucherLineCommand(credit, DebitCredit.Credit, amount, "طرف بستانکار")
                    ],
                    DocumentType.General, null, null, null, null, null, "user"));

                await engine.PostAsync(new PostVoucherCommand(
                    harness.TenantId, harness.CompanyId, draft.Id, null, "user"));
            }

            // --- ناوردا I1 پیش از اختتامیه ---
            var (preDebit, preCredit) = await SumGlAsync(harness, year1.Id);
            (preDebit - preCredit).Should().Be(0m, $"I1(seed {seed}): دفتر کل قبل از اختتامیه باید متوازن باشد");

            // --- مانده‌ها پیش از اختتامیه (اختتامیه موقت‌ها را صفر می‌کند) ---
            var permanentPre = new Dictionary<Guid, decimal>();
            foreach (var account in permanent)
                permanentPre[account.Id] = await SignedBalanceAsync(harness, account.Id, year1.Id);
            var revenuePre = await SignedBalanceAsync(harness, revenue.Id, year1.Id);
            var expensePre = await SignedBalanceAsync(harness, expense.Id, year1.Id);

            // --- اختتامیه ---
            var closingService = new FiscalClosingWorkflowService(harness.NewDbContext(), docs, engine);
            var closing = await closingService.RunClosingAsync(new RunFiscalClosingCommand(
                harness.TenantId, harness.CompanyId, year1.Id, retained.Id, "cfo"));

            closing.Status.Should().Be(FiscalClosingStatus.Closed, $"seed {seed}: اختتامیه دیتاست متوازن باید موفق باشد");

            // --- محاسبه مستقل سود/زیان از مانده‌های پیش از اختتامیه ---
            var expectedNetIncome = (-revenuePre) - expensePre; // بستانکار درآمد → منفی علامت‌دار
            (-revenuePre).Should().Be(closing.TotalRevenue, $"I3(seed {seed}): جمع درآمد");
            expensePre.Should().Be(closing.TotalExpense, $"I3(seed {seed}): جمع هزینه");
            closing.NetIncome.Should().Be(expectedNetIncome, $"I3(seed {seed}): سود/زیان = درآمد - هزینه");

            // --- ناوردا I2: موقت‌ها صفر ---
            (await SignedBalanceAsync(harness, revenue.Id, year1.Id)).Should().Be(0m, $"I2(seed {seed})");
            (await SignedBalanceAsync(harness, expense.Id, year1.Id)).Should().Be(0m, $"I2(seed {seed})");

            // --- ناوردا I3b: مانده حساب مقصد = مانده اولیه + اثر انتقال سود/زیان ---
            var retainedAfter = await SignedBalanceAsync(harness, retained.Id, year1.Id);
            retainedAfter.Should().Be(permanentPre[retained.Id] - expectedNetIncome,
                $"I3b(seed {seed}): مانده مقصد پس از انتقال نتیجه");

            // --- ناوردا I5: دائمی‌ها (غیر از مقصد) تغییر نکرده‌اند ---
            foreach (var account in permanent.Where(a => a.Id != retained.Id))
            {
                var after = await SignedBalanceAsync(harness, account.Id, year1.Id);
                after.Should().Be(permanentPre[account.Id],
                    $"I5(seed {seed}): مانده حساب دائمی {account.Code} پس از اختتامیه تغییر نکرده");
            }

            // --- ناوردا I1 پس از اختتامیه (سند اختتامیه هم متوازن) ---
            var (postDebit, postCredit) = await SumGlAsync(harness, year1.Id);
            (postDebit - postCredit).Should().Be(0m, $"I1(seed {seed}): دفتر کل پس از اختتامیه متوازن است");

            // --- افتتاحیه سال بعد ---
            var openingService = new OpeningVoucherService(
                harness.NewDbContext(), docs, engine, new EmptyOpeningInventorySource());
            var opening = await openingService.CreateOpeningVoucherAsync(new OpeningVoucherCommand(
                harness.TenantId, harness.CompanyId, year2.Id, "accountant"));

            opening.TotalDebit.Should().Be(opening.TotalCredit, $"I4(seed {seed}): افتتاحیه متوازن است");

            // --- ناوردا I4: مانده هر حساب دائمی در سال ۲ = مانده پس از اختتامیه سال ۱ ---
            foreach (var account in permanent)
            {
                var year1Balance = await SignedBalanceAsync(harness, account.Id, year1.Id);
                var year2Balance = await SignedBalanceAsync(harness, account.Id, year2.Id);
                year2Balance.Should().Be(year1Balance,
                    $"I4(seed {seed}): مانده {account.Code} در افتتاحیه ادامه یافته");
            }

            // موقت‌ها در سال ۲ هیچ گردشی ندارند
            (await SumGlAsync(harness, year2.Id)).Should().Be(
                (opening.TotalDebit, opening.TotalCredit), $"I4(seed {seed}): فقط خطوط افتتاحیه در سال ۲");
        }
        finally
        {
            harness.Dispose();
        }
    }

    private async Task<(decimal Debit, decimal Credit)> SumGlAsync(VoucherTestHarness harness, Guid fiscalYearId)
    {
        using var db = harness.NewDbContext();
        var rows = await db.GeneralLedgerEntries
            .Where(e => e.TenantId == harness.TenantId && e.CompanyId == harness.CompanyId && e.FiscalYearId == fiscalYearId)
            .Select(e => new { e.DebitAmount, e.CreditAmount })
            .ToListAsync();
        return (rows.Sum(r => r.DebitAmount), rows.Sum(r => r.CreditAmount));
    }

    private async Task<decimal> SignedBalanceAsync(VoucherTestHarness harness, Guid accountId, Guid fiscalYearId)
    {
        using var db = harness.NewDbContext();
        var rows = await db.GeneralLedgerEntries
            .Where(e => e.TenantId == harness.TenantId && e.CompanyId == harness.CompanyId
                        && e.FiscalYearId == fiscalYearId && e.AccountId == accountId)
            .Select(e => new { e.DebitAmount, e.CreditAmount })
            .ToListAsync();
        return rows.Sum(r => r.DebitAmount - r.CreditAmount);
    }
}
