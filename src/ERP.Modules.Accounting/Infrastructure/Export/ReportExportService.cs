using System.Globalization;
using ClosedXML.Excel;
using ERP.Modules.Accounting.Application.DTOs;
using ERP.Modules.Accounting.Domain.Enums;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Modules.Accounting.Infrastructure.Export;

public enum ReportExportFormat
{
    Excel = 1,
    Pdf = 2,
    Csv = 3
}

/// <summary>خروجی عمومی گزارش: متن و عدد (محاسبات مالی سمت Backend انجام شده است).</summary>
public sealed record ReportExportRequest(
    string Title,
    string ReportName,
    IReadOnlyList<ReportExportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    DateTimeOffset GeneratedAtUtc);

public sealed record ReportExportColumn(string Name, bool IsNumeric);

public sealed record ReportExportFile(
    string FileName,
    string ContentType,
    byte[] Content);

public interface IReportExportService
{
    Task<ReportExportFile> ExportAsync(ReportExportRequest request, ReportExportFormat format, CancellationToken ct = default);
}

/// <summary>
/// زیرساخت خروجی گزارش — Excel (RTL)، PDF (فارسی RTL با فونت وزیرمتن و تاریخ شمسی)
/// و CSV (UTF-8 BOM). همه اعداد از همان DTOهای سرویس گزارش می‌آیند تا خروجی
/// Excel/PDF دقیقاً با API JSON تطابق داشته باشد.
/// </summary>
public class ReportExportService : IReportExportService
{
    private const string FontFamily = "Vazirmatn";

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = true;
        TryRegisterFonts();
    }

    public async Task<ReportExportFile> ExportAsync(ReportExportRequest request, ReportExportFormat format, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return format switch
        {
            ReportExportFormat.Excel => BuildExcel(request),
            ReportExportFormat.Pdf => BuildPdf(request),
            ReportExportFormat.Csv => BuildCsv(request),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    // ------------------------------------------------------------------
    // Excel — ClosedXML (RTL، اعداد عددی، سرستون بولد)
    // ------------------------------------------------------------------

    private static ReportExportFile BuildExcel(ReportExportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(request.Title));

        sheet.RightToLeft = true;
        sheet.SheetView.FreezeRows(1);

        for (var c = 0; c < request.Columns.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = request.Columns[c].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
        }

        for (var r = 0; r < request.Rows.Count; r++)
        {
            var row = request.Rows[r];
            for (var c = 0; c < row.Count && c < request.Columns.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                if (row[c] is decimal number)
                {
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "#,##0.####";
                }
                else
                {
                    cell.Value = row[c]?.ToString() ?? string.Empty;
                }
            }
        }

        sheet.Columns().AdjustToContents(1, Math.Min(request.Rows.Count + 1, 200));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportExportFile(
            $"{request.ReportName}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    // ------------------------------------------------------------------
    // PDF — QuestPDF (فارسی RTL، فونت وزیرمتن، تاریخ شمسی)
    // ------------------------------------------------------------------

    private static ReportExportFile BuildPdf(ReportExportRequest request)
    {
        var jalaliDate = FormatJalali(request.GeneratedAtUtc);
        var numericColumnCount = request.Columns.Count(c => c.IsNumeric);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(numericColumnCount > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().AlignCenter().Text(request.Title).Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"تاریخ صدور: {jalaliDate}").FontSize(9);
                    column.Item().PaddingBottom(6);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var columnDef in request.Columns)
                        {
                            columns.RelativeColumn(columnDef.IsNumeric ? 2f : 3f);
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var columnDef in request.Columns)
                        {
                            header.Cell().Background("#E8EEF7").Padding(4)
                                .Text(text =>
                                {
                                    text.DefaultTextStyle(x => x.SemiBold());
                                    text.Span(columnDef.Name);
                                });
                        }
                    });

                    foreach (var row in request.Rows)
                    {
                        for (var c = 0; c < request.Columns.Count; c++)
                        {
                            var value = c < row.Count ? row[c] : null;
                            table.Cell().Padding(3).Text(text =>
                            {
                                if (value is decimal number)
                                {
                                    text.DefaultTextStyle(x => x.FontSize(8.5f));
                                    text.Span(number.ToString("N2", CultureInfo.InvariantCulture));
                                }
                                else
                                {
                                    text.Span(value?.ToString() ?? string.Empty);
                                }
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("صفحه ");
                    text.CurrentPageNumber();
                    text.Span(" از ");
                    text.TotalPages();
                });
            });
        });

        var pdf = document.GeneratePdf();
        return new ReportExportFile($"{request.ReportName}.pdf", "application/pdf", pdf);
    }

    // ------------------------------------------------------------------
    // CSV — UTF-8 BOM (سازگار با Excel فارسی)
    // ------------------------------------------------------------------

    private static ReportExportFile BuildCsv(ReportExportRequest request)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine(string.Join(",",
            request.Columns.Select(c => EscapeCsv(c.Name))));

        foreach (var row in request.Rows)
        {
            builder.AppendLine(string.Join(",",
                row.Take(request.Columns.Count).Select(v => v is decimal number
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : EscapeCsv(v?.ToString() ?? string.Empty))));
        }

        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString()))
            .ToArray();

        return new ReportExportFile($"{request.ReportName}.csv", "text/csv; charset=utf-8", bytes);
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static string SanitizeSheetName(string title)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var name = new string(title.Where(ch => !invalid.Contains(ch)).ToArray());
        return string.IsNullOrWhiteSpace(name) ? "Report" : name.Length > 30 ? name[..30] : name;
    }

    /// <summary>تاریخ شمسی برای سربرگ گزارش‌ها.</summary>
    private static string FormatJalali(DateTimeOffset utc)
    {
        var persian = new PersianCalendar();
        var local = utc.ToLocalTime().DateTime;
        return $"{persian.GetYear(local):0000}/{persian.GetMonth(local):00}/{persian.GetDayOfMonth(local):00}";
    }

    private static void TryRegisterFonts()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Vazirmatn-Regular.ttf"),
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    FontManager.RegisterFont(stream);
                }
                catch
                {
                    // فونت تکراری ثبت شده؛ ادامه
                }

                break;
            }
        }
    }
}

// ===========================================================================
// نگاشت DTOهای گزارش به ستون‌ها و ردیف‌های خروجی
// ===========================================================================

/// <summary>
/// تبدیل نتیجه سرویس‌های گزارش به ساختار خروجی — همان داده‌ای که API JSON
/// برمی‌گرداند؛ خروجی Excel/PDF/CSV با JSON تطابق کامل دارد.
/// </summary>
public static class ReportExportMapper
{
    public static ReportExportRequest FromTrialBalance(TrialBalanceResultDto report) =>
        new(
            $"تراز آزمایشی ({AggregationLevelTitle(report.AggregationLevel)}) — از {FormatJalali(report.FromDate)} تا {FormatJalali(report.ToDate)}",
            "TrialBalance",
            Columns("کد حساب", "شرح حساب", "مانده اول دوره بدهکار", "مانده اول دوره بستانکار",
                "گردش بدهکار", "گردش بستانکار", "مانده پایان دوره بدهکار", "مانده پایان دوره بستانکار"),
            report.Rows.Select(r => Row<object?>(
                r.AccountCode, r.AccountName,
                r.OpeningDebit, r.OpeningCredit, r.PeriodDebit, r.PeriodCredit,
                r.ClosingDebit, r.ClosingCredit)).ToList(),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromGeneralLedger(IReadOnlyList<GeneralLedgerRowDto> rows) =>
        new(
            "دفتر کل",
            "GeneralLedger",
            Columns("کد حساب", "شرح حساب", "مانده اول دوره بدهکار", "مانده اول دوره بستانکار",
                "گردش بدهکار", "گردش بستانکار", "مانده پایان دوره بدهکار", "مانده پایان دوره بستانکار"),
            rows.Select(r => Row<object?>(
                r.AccountCode, r.AccountName,
                r.OpeningDebit, r.OpeningCredit, r.DebitTurnover, r.CreditTurnover,
                r.ClosingDebit, r.ClosingCredit)).ToList(),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromJournal(IReadOnlyList<JournalRowDto> rows) =>
        new(
            "دفتر روزنامه",
            "Journal",
            Columns("شماره سند", "تاریخ", "کد حساب", "شرح حساب", "شرح ردیف", "بدهکار", "بستانکار", "ارز"),
            rows.Select(r => Row<object?>(
                r.DocumentNumber.ToString(), FormatJalali(r.EntryDate),
                r.AccountCode, r.AccountName, r.Description, r.Debit, r.Credit, r.CurrencyCode)).ToList(),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromBalanceSheet(BalanceSheetDto report) =>
        new(
            $"ترازنامه تا {FormatJalali(report.AsOfDate)}",
            "BalanceSheet",
            Columns("بخش", "کد حساب", "شرح حساب", "مانده"),
            Sections(report.CurrentAssets, report.NonCurrentAssets, report.CurrentLiabilities, report.NonCurrentLiabilities, report.Equity),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromProfitLoss(ProfitLossDto report) =>
        new(
            $"صورت سود و زیان از {FormatJalali(report.FromDate)} تا {FormatJalali(report.ToDate)}",
            "ProfitLoss",
            Columns("بخش", "کد حساب", "شرح حساب", "مانده"),
            Sections(report.Revenues, report.CostOfGoodsSold, report.OperatingExpenses, report.OtherIncome, report.OtherExpenses, report.TaxExpenses),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromCashFlow(CashFlowDto report) =>
        new(
            $"گزارش جریان وجوه نقد از {FormatJalali(report.FromDate)} تا {FormatJalali(report.ToDate)}",
            "CashFlow",
            Columns("سبد جریان نقد", "ورود", "خروج", "خالص"),
            report.Buckets.Select(b => Row<object?>(BucketTitle(b.Category), b.Inflow, b.Outflow, b.Net)).ToList(),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromPartyLedger(PartyLedgerResultDto report) =>
        new(
            report.Title,
            "PartyLedger",
            Columns("شناسه شخص", "شرح شخص", "مانده اول بدهکار", "مانده اول بستانکار",
                "گردش بدهکار", "گردش بستانکار", "مانده پایان بدهکار", "مانده پایان بستانکار", "مانده علامت‌دار"),
            report.Rows.Select(r => Row<object?>(
                r.DimensionId.ToString(), r.DimensionLabel,
                r.OpeningDebit, r.OpeningCredit, r.DebitTurnover, r.CreditTurnover,
                r.ClosingDebit, r.ClosingCredit, r.SignedBalance)).ToList(),
            DateTimeOffset.UtcNow);

    public static ReportExportRequest FromAccountCard(AccountCardResultDto card) =>
        new(
            $"کارت حساب {card.AccountCode} - {card.AccountName}",
            "AccountCard",
            Columns("تاریخ", "شماره سند", "شرح", "بدهکار", "بستانکار", "مانده", "طرف مانده"),
            card.Rows.Select(r => Row<object?>(
                FormatJalali(r.EntryDate), r.DocumentNumber.ToString(), r.Description,
                r.Debit, r.Credit, r.RunningBalance,
                r.BalanceSide == "Debit" ? "بدهکار" : "بستانکار")).ToList(),
            DateTimeOffset.UtcNow);

    private static List<IReadOnlyList<object?>> Sections(params StatementSectionDto[] sections)
    {
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var section in sections)
        {
            rows.Add(Row<object?>($"[{section.Title}]", string.Empty, string.Empty, section.Total));
            rows.AddRange(section.Rows.Select(r => Row<object?>(string.Empty, r.AccountCode, r.AccountName, r.Balance)));
        }

        return rows;
    }

    private static ReportExportColumn[] Columns(params string[] names) =>
        names.Select((name, i) => new ReportExportColumn(name, IsNumeric: i >= 2)).ToArray();

    private static IReadOnlyList<object?> Row<T>(params object?[] values) => values;

    private static string BucketTitle(CashFlowCategory category) => category switch
    {
        CashFlowCategory.Operating => "جریان عملیاتی",
        CashFlowCategory.Investing => "جریان سرمایه‌گذاری",
        CashFlowCategory.Financing => "جریان تأمین مالی",
        _ => category.ToString()
    };

    private static string AggregationLevelTitle(AccountLevel level) => level switch
    {
        AccountLevel.Group => "یک‌سطحی (گروه)",
        AccountLevel.General => "دوسطحی (کل)",
        AccountLevel.Subsidiary => "سه‌سطحی (معین)",
        AccountLevel.Detail => "چهارسطحی (تفصیلی)",
        _ => level.ToString()
    };

    private static string FormatJalali(DateOnly date)
    {
        var persian = new PersianCalendar();
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return $"{persian.GetYear(dateTime):0000}/{persian.GetMonth(dateTime):00}/{persian.GetDayOfMonth(dateTime):00}";
    }
}
