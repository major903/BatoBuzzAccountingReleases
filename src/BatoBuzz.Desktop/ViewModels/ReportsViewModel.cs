using BatoBuzz.Application.Interfaces;
using BatoBuzz.Desktop.Services;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace BatoBuzz.Desktop.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IAccountingService _accountingService;
    private readonly ISalesService _salesService;
    private readonly IPurchaseService _purchaseService;
    private readonly IInventoryService _inventoryService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private string _selectedReport = "Trial Balance";

    [ObservableProperty]
    private bool _isLedgerFilterVisible;

    [ObservableProperty]
    private ObservableCollection<string> _ledgerNames = new();

    [ObservableProperty]
    private string _selectedLedgerName = "";

    [ObservableProperty]
    private DateTime _fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

    [ObservableProperty]
    private DateTime _toDate = DateTime.Now;

    [ObservableProperty]
    private string _reportHtml = "";

    [ObservableProperty]
    private bool _isLoading;

    public string[] AvailableReports { get; } =
    {
        "Chart of Accounts",
        "Day Book",
        "General Ledger",
        "Trial Balance",
        "Profit & Loss",
        "Balance Sheet",
        "Cash Flow",
        "Sales Register",
        "Purchase Register",
        "Receivables Ageing",
        "Payables Ageing",
        "Stock Summary"
    };

    public ReportsViewModel(
        IAccountingService accountingService,
        ISalesService salesService,
        IPurchaseService purchaseService,
        IInventoryService inventoryService,
        DesktopDataService dataService,
        DesktopSession session)
    {
        _accountingService = accountingService;
        _salesService = salesService;
        _purchaseService = purchaseService;
        _inventoryService = inventoryService;
        _dataService = dataService;
        _session = session;
    }

    partial void OnSelectedReportChanged(string value) =>
        IsLedgerFilterVisible = value == "General Ledger";

    partial void OnIsLoadingChanged(bool value)
    {
        GenerateReportCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    partial void OnReportHtmlChanged(string value)
    {
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    private bool CanGenerateReport() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanGenerateReport))]
    private async Task GenerateReport()
    {
        if (ToDate.Date < FromDate.Date)
        {
            ReportHtml = WrapHtml("<h2>Choose a valid date range</h2><p>The end date must be the same as or later than the start date.</p>");
            return;
        }

        if (!_session.CompanyId.HasValue)
        {
            ReportHtml = WrapHtml("<h2>No company selected</h2><p>Select or create a company, then generate the report again.</p>");
            return;
        }

        IsLoading = true;
        try
        {
            var companyId = _session.CompanyId.Value;
            await LoadLedgerNamesAsync(companyId);
            ReportHtml = SelectedReport switch
            {
                "Chart of Accounts" => WrapHtml(await ChartOfAccountsHtml(companyId)),
                "Day Book" => WrapHtml(await DayBookHtml(companyId)),
                "General Ledger" => WrapHtml(await GeneralLedgerHtml(companyId)),
                "Trial Balance" => WrapHtml(await TrialBalanceHtml(companyId)),
                "Profit & Loss" => WrapHtml(await ProfitLossHtml(companyId)),
                "Balance Sheet" => WrapHtml(await BalanceSheetHtml(companyId)),
                "Cash Flow" => WrapHtml(await CashFlowHtml(companyId)),
                "Sales Register" => WrapHtml(await SalesRegisterHtml(companyId)),
                "Purchase Register" => WrapHtml(await PurchaseRegisterHtml(companyId)),
                "Receivables Ageing" => WrapHtml(await ReceivablesHtml(companyId)),
                "Payables Ageing" => WrapHtml(await PayablesHtml(companyId)),
                "Stock Summary" => WrapHtml(await StockSummaryHtml(companyId)),
                _ => WrapHtml("<h2>Report not available</h2>")
            };
        }
        catch (Exception ex)
        {
            ReportHtml = WrapHtml($"<h2>Error</h2><p>{Html(ex.Message)}</p>");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLedgerNamesAsync(Guid companyId)
    {
        var ledgers = await _dataService.GetLedgersAsync(companyId);
        var current = SelectedLedgerName;
        LedgerNames.Clear();
        foreach (var ledger in ledgers.OrderBy(l => l.AccountGroup.AccountType).ThenBy(l => l.AccountGroup.Name).ThenBy(l => l.Name))
            LedgerNames.Add(ledger.Name);

        SelectedLedgerName = !string.IsNullOrWhiteSpace(current) && LedgerNames.Contains(current)
            ? current
            : LedgerNames.FirstOrDefault() ?? "";
    }

    private async Task<string> ChartOfAccountsHtml(Guid companyId)
    {
        var ledgers = await _dataService.GetLedgersAsync(companyId);
        var rows = ledgers
            .OrderBy(l => l.AccountGroup.AccountType)
            .ThenBy(l => l.AccountGroup.Name)
            .ThenBy(l => l.Name)
            .Select(l => Row(
                Html(l.AccountGroup.AccountType.ToString()),
                Html(l.AccountGroup.Name),
                Html(l.Name),
                Html(l.LedgerType.ToString()),
                Money(l.OpeningBalance),
                Money(l.CurrentBalance),
                l.IsActive ? "Active" : "Inactive"));

        return Heading("Chart of Accounts") +
            Table(new[] { "Type", "Group", "Ledger", "Ledger Type", "Opening", "Current", "Status" }, rows);
    }

    private async Task<string> DayBookHtml(Guid companyId)
    {
        var entries = await _dataService.GetJournalEntriesAsync(companyId, FromDate, ToDate);
        var rows = entries
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.EntryNumber)
            .Select(e => Row(
                e.EntryDate.ToShortDateString(),
                Html(e.VoucherType.ToString()),
                Html(e.EntryNumber),
                Html(e.ReferenceNumber ?? ""),
                Html(e.Narration ?? ""),
                Money(e.TotalDebit),
                Html(e.Status.ToString())));

        return Heading("Day Book") +
            Table(new[] { "Date", "Voucher", "Number", "Reference", "Narration", "Amount", "Status" }, rows);
    }

    private async Task<string> GeneralLedgerHtml(Guid companyId)
    {
        var ledgers = await _dataService.GetLedgersAsync(companyId);
        var ledger = ledgers.FirstOrDefault(l => string.Equals(l.Name, SelectedLedgerName, StringComparison.OrdinalIgnoreCase))
            ?? ledgers.OrderBy(l => l.Name).FirstOrDefault();

        if (ledger == null)
            return Heading("General Ledger") + "<p>No ledgers found.</p>";

        var report = await _accountingService.GetGeneralLedgerAsync(companyId, ledger.Id, FromDate, ToDate);
        var rows = report.Transactions.Select(t => Row(
            t.Date.ToShortDateString(),
            Html(t.VoucherType),
            Html(t.EntryNumber),
            Html(t.Narration ?? ""),
            Money(t.Debit),
            Money(t.Credit),
            Money(t.Balance)));

        return Heading($"General Ledger - {Html(report.LedgerName)}") +
            $"<h3>Opening {Money(report.OpeningBalance)} | Closing {Money(report.ClosingBalance)}</h3>" +
            Table(new[] { "Date", "Voucher", "Number", "Narration", "Debit", "Credit", "Balance" }, rows);
    }

    private async Task<string> CashFlowHtml(Guid companyId)
    {
        var ledgers = await _dataService.GetLedgersAsync(companyId);
        var cashLedgers = ledgers
            .Where(l => l.LedgerType is BatoBuzz.Domain.Enums.LedgerType.Cash or BatoBuzz.Domain.Enums.LedgerType.Bank)
            .OrderBy(l => l.Name)
            .ToList();

        var rows = new List<string>();
        decimal totalInflow = 0;
        decimal totalOutflow = 0;

        foreach (var ledger in cashLedgers)
        {
            var report = await _accountingService.GetGeneralLedgerAsync(companyId, ledger.Id, FromDate, ToDate);
            var inflow = report.Transactions.Sum(t => t.Debit);
            var outflow = report.Transactions.Sum(t => t.Credit);
            totalInflow += inflow;
            totalOutflow += outflow;
            rows.Add(Row(Html(ledger.Name), Money(report.OpeningBalance), Money(inflow), Money(outflow), Money(report.ClosingBalance)));
        }

        return Heading("Cash Flow") +
            Table(new[] { "Cash/Bank Ledger", "Opening", "Inflow", "Outflow", "Closing" }, rows) +
            $"<h3>Total Inflow {Money(totalInflow)} | Total Outflow {Money(totalOutflow)} | Net Flow {Money(totalInflow - totalOutflow)}</h3>";
    }

    private bool CanExport() => !string.IsNullOrWhiteSpace(ReportHtml) && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportPdf()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export report as PDF",
            FileName = $"{SelectedReport.Replace("&", "and").Replace(' ', '-')}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf",
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var report = ParseHtmlReport(ReportHtml);
            WriteReportPdf(report, dialog.FileName);
            MessageBox.Show($"PDF report exported:{Environment.NewLine}{dialog.FileName}", "BatoBuzz Reports",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportExcel()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export report as Excel",
            FileName = $"{SelectedReport.Replace("&", "and").Replace(' ', '-')}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var report = ParseHtmlReport(ReportHtml);
            WriteReportExcel(report, dialog.FileName);
            MessageBox.Show($"Excel report exported:{Environment.NewLine}{dialog.FileName}", "BatoBuzz Reports",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    private static void ShowExportError(Exception exception) =>
        MessageBox.Show(
            $"The report could not be exported. Check that the chosen file is not open in another program and that you can write to its folder.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
            "BatoBuzz Reports",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    private async Task<string> TrialBalanceHtml(Guid companyId)
    {
        var report = await _accountingService.GetTrialBalanceAsync(companyId, FromDate, ToDate);
        var rows = report.Items.Select(i => Row(Html(i.LedgerName), i.AccountType.ToString(), Money(i.OpeningDebit), Money(i.OpeningCredit), Money(i.PeriodDebit), Money(i.PeriodCredit), Money(i.ClosingDebit), Money(i.ClosingCredit)));
        return Heading("Trial Balance") + Table(new[] { "Ledger", "Type", "Opening Dr", "Opening Cr", "Period Dr", "Period Cr", "Closing Dr", "Closing Cr" }, rows)
            + $"<h3>Total Dr {Money(report.TotalDebit)} | Total Cr {Money(report.TotalCredit)}</h3>";
    }

    private async Task<string> ProfitLossHtml(Guid companyId)
    {
        var report = await _accountingService.GetProfitAndLossAsync(companyId, FromDate, ToDate);
        var rows = report.IncomeAccounts.Select(a => Row(Html(a.LedgerName), Money(a.Amount)))
            .Concat(report.CostOfSalesAccounts.Select(a => Row(Html(a.LedgerName), Money(-a.Amount))))
            .Concat(report.ExpenseAccounts.Select(a => Row(Html(a.LedgerName), Money(-a.Amount))));
        return Heading("Profit & Loss") + Table(new[] { "Account", "Amount" }, rows)
            + $"<h3>Gross Profit {Money(report.GrossProfit)} | Net Profit {Money(report.NetProfit)}</h3>";
    }

    private async Task<string> BalanceSheetHtml(Guid companyId)
    {
        var report = await _accountingService.GetBalanceSheetAsync(companyId, ToDate);
        var rows = report.Assets.Select(a => Row("Asset", Html(a.LedgerName), Money(a.Amount)))
            .Concat(report.Liabilities.Select(a => Row("Liability", Html(a.LedgerName), Money(a.Amount))))
            .Concat(report.Equity.Select(a => Row("Equity", Html(a.LedgerName), Money(a.Amount))));
        return Heading("Balance Sheet") + Table(new[] { "Type", "Account", "Amount" }, rows)
            + $"<h3>Assets {Money(report.TotalAssets)} | Liabilities {Money(report.TotalLiabilities)} | Equity {Money(report.TotalEquity)}</h3>";
    }

    private async Task<string> SalesRegisterHtml(Guid companyId)
    {
        var invoices = await _salesService.GetInvoicesAsync(companyId, FromDate, ToDate);
        var rows = invoices.Select(i => Row(i.InvoiceDate.ToShortDateString(), Html(i.InvoiceNumber), Html(i.CustomerName), Money(i.TotalAmount), i.Status.ToString()));
        return Heading("Sales Register") + Table(new[] { "Date", "Invoice", "Customer", "Amount", "Status" }, rows);
    }

    private async Task<string> PurchaseRegisterHtml(Guid companyId)
    {
        var bills = await _purchaseService.GetBillsAsync(companyId, FromDate, ToDate);
        var rows = bills.Select(b => Row(b.BillDate.ToShortDateString(), Html(b.BillNumber), Html(b.SupplierName), Money(b.TotalAmount), b.Status.ToString()));
        return Heading("Purchase Register") + Table(new[] { "Date", "Bill", "Supplier", "Amount", "Status" }, rows);
    }

    private async Task<string> ReceivablesHtml(Guid companyId)
    {
        var items = await _salesService.GetReceivablesAgeingAsync(companyId, ToDate);
        var rows = items.Select(i => Row(Html(i.ContactName), Money(i.CurrentAmount), Money(i.Days1To30), Money(i.Days31To60), Money(i.Days61To90), Money(i.Over90Days), Money(i.TotalAmount)));
        return Heading("Receivables Ageing") + Table(new[] { "Customer", "Current", "1-30", "31-60", "61-90", "Over 90", "Total" }, rows);
    }

    private async Task<string> PayablesHtml(Guid companyId)
    {
        var items = await _purchaseService.GetPayablesAgeingAsync(companyId, ToDate);
        var rows = items.Select(i => Row(Html(i.ContactName), Money(i.CurrentAmount), Money(i.Days1To30), Money(i.Days31To60), Money(i.Days61To90), Money(i.Over90Days), Money(i.TotalAmount)));
        return Heading("Payables Ageing") + Table(new[] { "Supplier", "Current", "1-30", "31-60", "61-90", "Over 90", "Total" }, rows);
    }

    private async Task<string> StockSummaryHtml(Guid companyId)
    {
        var items = await _inventoryService.GetInventoryReportAsync(companyId);
        var rows = items.Select(i => Row(Html(i.ItemName), Html(i.WarehouseName ?? ""), i.Quantity.ToString("N2"), Money(i.AverageCost), Money(i.TotalValue), i.IsLowStock ? "Low" : ""));
        return Heading("Stock Summary") + Table(new[] { "Item", "Warehouse", "Qty", "Avg Cost", "Value", "Status" }, rows);
    }

    private static string Heading(string title) => $"<h2>{Html(title)}</h2>";

    private static string Table(string[] headers, IEnumerable<string> rows)
    {
        var sb = new StringBuilder("<table><thead><tr>");
        foreach (var header in headers)
            sb.Append("<th>").Append(Html(header)).Append("</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in rows)
            sb.Append(row);
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private static string Row(params string[] cells) =>
        "<tr>" + string.Concat(cells.Select(c => $"<td>{c}</td>")) + "</tr>";

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private sealed record ParsedReport(string Title, IReadOnlyList<string> Summaries, string[] Headers, List<string[]> Rows);

    private static ParsedReport ParseHtmlReport(string html)
    {
        var titleMatch = Regex.Match(html, "<h2>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var title = titleMatch.Success ? CleanCellText(titleMatch.Groups[1].Value) : "Report";

        var summaries = Regex.Matches(html, "<h3>(.*?)</h3>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Cast<Match>()
            .Select(m => CleanCellText(m.Groups[1].Value))
            .ToList();

        var tableRows = Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Cast<Match>()
            .Select(row => Regex.Matches(row.Groups[1].Value, "<t[dh]>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Cast<Match>()
                .Select(c => CleanCellText(c.Groups[1].Value))
                .ToArray())
            .ToList();

        var headers = tableRows.Count > 0 ? tableRows[0] : Array.Empty<string>();
        var dataRows = tableRows.Count > 1 ? tableRows.Skip(1).ToList() : new List<string[]>();

        return new ParsedReport(title, summaries, headers, dataRows);
    }

    private static string CleanCellText(string cellHtml) =>
        WebUtility.HtmlDecode(Regex.Replace(cellHtml, "<.*?>", string.Empty)).Trim();

    private static void WriteReportExcel(ParsedReport report, string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheetName = report.Title.Length > 31 ? report.Title.Substring(0, 31) : report.Title;
        var ws = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Report" : sheetName);

        var r = 1;
        ws.Cell(r, 1).Value = report.Title;
        ws.Cell(r, 1).Style.Font.Bold = true;
        ws.Cell(r, 1).Style.Font.FontSize = 14;
        r += 2;

        for (var c = 0; c < report.Headers.Length; c++)
        {
            var cell = ws.Cell(r, c + 1);
            cell.Value = report.Headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEEEF7");
        }
        r++;

        foreach (var row in report.Rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                if (decimal.TryParse(row[c], NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "#,##0.00";
                }
                else
                {
                    cell.Value = row[c];
                }
            }
            r++;
        }

        if (report.Summaries.Count > 0)
        {
            r++;
            foreach (var summary in report.Summaries)
            {
                ws.Cell(r, 1).Value = summary;
                ws.Cell(r, 1).Style.Font.Bold = true;
                r++;
            }
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    private static void WriteReportPdf(ParsedReport report, string filePath)
    {
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        const double margin = 36;
        var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
        var summaryFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 9, XFontStyleEx.Bold);
        var cellFont = new XFont("Arial", 8.5, XFontStyleEx.Regular);

        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        double y = margin;
        double pageWidth = page.Width.Point - 2 * margin;
        double pageHeight = page.Height.Point;

        gfx.DrawString(report.Title, titleFont, XBrushes.Black, new XPoint(margin, y + 16));
        y += 30;
        gfx.DrawString($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}", cellFont, XBrushes.Gray, new XPoint(margin, y));
        y += 20;

        var columnCount = Math.Max(report.Headers.Length, 1);
        const double rowHeight = 18;

        // The first column usually holds names/descriptions and needs more room
        // than the numeric columns that follow.
        var weights = new double[columnCount];
        for (var i = 0; i < columnCount; i++)
            weights[i] = i == 0 ? 2.0 : 1.0;
        var totalWeight = weights.Sum();
        var columnOffsets = new double[columnCount + 1];
        for (var i = 0; i < columnCount; i++)
            columnOffsets[i + 1] = columnOffsets[i] + pageWidth * weights[i] / totalWeight;

        void DrawRow(string[] cells, XFont font, bool shaded)
        {
            if (y + rowHeight > pageHeight - margin)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
            }

            if (shaded)
                gfx.DrawRectangle(XBrushes.WhiteSmoke, margin, y, pageWidth, rowHeight);

            for (var c = 0; c < cells.Length && c < columnCount; c++)
            {
                var colWidth = columnOffsets[c + 1] - columnOffsets[c];
                var rect = new XRect(margin + columnOffsets[c] + 3, y + 2, colWidth - 6, rowHeight - 4);
                gfx.Save();
                gfx.IntersectClip(rect);
                gfx.DrawString(cells[c], font, XBrushes.Black, rect, XStringFormats.TopLeft);
                gfx.Restore();
            }

            gfx.DrawLine(XPens.LightGray, margin, y + rowHeight, margin + pageWidth, y + rowHeight);
            y += rowHeight;
        }

        if (report.Headers.Length > 0)
            DrawRow(report.Headers, headerFont, shaded: true);

        foreach (var row in report.Rows)
            DrawRow(row, cellFont, shaded: false);

        if (report.Summaries.Count > 0)
        {
            y += 10;
            foreach (var summary in report.Summaries)
            {
                if (y + rowHeight > pageHeight - margin)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }
                gfx.DrawString(summary, summaryFont, XBrushes.Black, new XPoint(margin, y + 12));
                y += rowHeight;
            }
        }

        document.Save(filePath);
    }

    private static string WrapHtml(string body) =>
        "<!doctype html><html><head><meta charset=\"utf-8\">" +
        "<style>" +
        "body{font-family:Segoe UI,Arial,sans-serif;background:#FFFFFF;color:#172033;margin:16px;}" +
        "h2{margin-top:0;color:#0F766E;}" +
        "table{width:100%;border-collapse:collapse;}" +
        "th,td{border:1px solid #9CA3AF;padding:8px;text-align:left;}" +
        "th{background:#DDF7F4;color:#0F4E49;}" +
        "td:nth-child(n+3){text-align:right;}" +
        "</style></head><body>" + body + "</body></html>";
}
