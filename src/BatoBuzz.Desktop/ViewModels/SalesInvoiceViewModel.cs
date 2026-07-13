using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace BatoBuzz.Desktop.ViewModels;

public partial class SalesInvoiceViewModel : ObservableObject
{
    private readonly ISalesService _salesService;
    private readonly DesktopDataService _dataService;
    private readonly ICompanyService _companyService;
    private readonly DesktopSession _session;
    private Guid? _savedInvoiceId;
    private InvoicePrintSnapshot? _printSnapshot;

    [ObservableProperty]
    private string _invoiceNumber = "";

    [ObservableProperty]
    private DateTime _invoiceDate = DateTime.Now;

    [ObservableProperty]
    private string _invoiceDateBs = BikramSambatConverter.IsSupported(DateTime.Now) ? BikramSambatConverter.ToBsDisplayString(DateTime.Now) : "";

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Now.AddDays(30);

    [ObservableProperty]
    private string _customerName = "";

    [ObservableProperty]
    private string _reference = "";

    [ObservableProperty]
    private string _narration = "";

    [ObservableProperty]
    private decimal _subTotal;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _vatAmount;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private bool _isVatApplicable = true;

    [ObservableProperty]
    private decimal _vatRate = 13;

    [ObservableProperty]
    private ObservableCollection<InvoiceLineViewModel> _lines = new();

    [ObservableProperty]
    private ObservableCollection<Item> _availableItems = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableCustomers = new();

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isDocumentEditable = true;

    [ObservableProperty]
    private bool _canPrint;

    [ObservableProperty]
    private bool _canPost = true;

    [ObservableProperty]
    private bool _isPosted;

    public SalesInvoiceViewModel(ISalesService salesService, DesktopDataService dataService, ICompanyService companyService, DesktopSession session)
    {
        _salesService = salesService;
        _dataService = dataService;
        _companyService = companyService;
        _session = session;
        AddLine();
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        var items = await _dataService.GetItemsAsync(_session.CompanyId.Value);
        AvailableItems = new ObservableCollection<Item>(items.Where(i => i.IsActive).OrderBy(i => i.Name));

        var customers = await _dataService.GetCustomersAsync(_session.CompanyId.Value);
        AvailableCustomers = new ObservableCollection<string>(customers.Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => c.Name));
    }

    partial void OnInvoiceDateChanged(DateTime value) =>
        InvoiceDateBs = BikramSambatConverter.IsSupported(value) ? BikramSambatConverter.ToBsDisplayString(value) : "";

    [RelayCommand]
    private void AddLine()
    {
        if (!IsDocumentEditable)
            return;

        var line = new InvoiceLineViewModel { LineNumber = Lines.Count + 1 };
        line.PropertyChanged += OnLinePropertyChanged;
        Lines.Add(line);
        CalculateTotals();
    }

    [RelayCommand]
    private void RemoveLine(InvoiceLineViewModel line)
    {
        if (IsDocumentEditable && Lines.Count > 1)
        {
            line.PropertyChanged -= OnLinePropertyChanged;
            Lines.Remove(line);
            RecalculateLineNumbers();
            CalculateTotals();
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        await SaveInvoiceAsync(post: false);
    }

    [RelayCommand]
    private async Task Post()
    {
        if (!CanPost)
        {
            StatusMessage = $"Invoice {InvoiceNumber} is already posted. Select New Invoice to start another.";
            return;
        }

        var result = MessageBox.Show("Are you sure you want to post this invoice? It cannot be edited later.", "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await SaveInvoiceAsync(post: true);
        }
    }

    [RelayCommand]
    private void NewInvoice()
    {
        foreach (var line in Lines)
            line.PropertyChanged -= OnLinePropertyChanged;

        Lines.Clear();
        _savedInvoiceId = null;
        _printSnapshot = null;
        InvoiceNumber = "";
        InvoiceDate = DateTime.Now;
        DueDate = DateTime.Now.AddDays(30);
        CustomerName = "";
        Reference = "";
        Narration = "";
        IsVatApplicable = true;
        VatRate = 13;
        SubTotal = 0;
        DiscountAmount = 0;
        VatAmount = 0;
        TotalAmount = 0;
        IsDocumentEditable = true;
        CanPrint = false;
        CanPost = true;
        IsPosted = false;
        StatusMessage = "Ready for a new invoice.";
        AddLine();
    }

    [RelayCommand]
    private async Task Print()
    {
        var snapshot = _printSnapshot;
        if (!_savedInvoiceId.HasValue || snapshot is null)
        {
            StatusMessage = "Save the invoice before printing.";
            return;
        }

        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        try
        {
            var company = await _companyService.GetCompanyAsync(_session.CompanyId.Value);
            if (company == null)
            {
                StatusMessage = "Company details could not be loaded.";
                return;
            }

            var fileSafeNumber = snapshot.InvoiceNumber;
            var tempPath = Path.Combine(Path.GetTempPath(), $"BatoBuzz-Invoice-{fileSafeNumber}-{Guid.NewGuid():N}.pdf");
            WriteInvoicePdf(company, snapshot, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            StatusMessage = $"Opened invoice for printing ({fileSafeNumber}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not print invoice: {ex.Message}";
        }
    }

    private void WriteInvoicePdf(CompanyDto company, InvoicePrintSnapshot snapshot, string filePath)
    {
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        const double margin = 40;
        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var companyFont = new XFont("Arial", 13, XFontStyleEx.Bold);
        var labelFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var valueFont = new XFont("Arial", 9, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 9, XFontStyleEx.Bold);
        var cellFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var totalFont = new XFont("Arial", 11, XFontStyleEx.Bold);

        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        double pageWidth = page.Width.Point - 2 * margin;
        double y = margin;

        gfx.DrawString(company.TradingName ?? company.Name, companyFont, XBrushes.Black, new XPoint(margin, y + 14));
        y += 20;
        var companyDetails = string.Join(", ", new[] { company.Address, company.City, company.Province }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(companyDetails))
        {
            gfx.DrawString(companyDetails, labelFont, XBrushes.Black, new XPoint(margin, y + 10));
            y += 14;
        }
        var companyRegNumbers = string.Join("   ", new[]
        {
            string.IsNullOrWhiteSpace(company.PanNumber) ? null : $"PAN: {company.PanNumber}",
            string.IsNullOrWhiteSpace(company.VatNumber) ? null : $"VAT: {company.VatNumber}"
        }.Where(s => s != null));
        if (!string.IsNullOrWhiteSpace(companyRegNumbers))
        {
            gfx.DrawString(companyRegNumbers, labelFont, XBrushes.Black, new XPoint(margin, y + 10));
            y += 14;
        }

        y += 12;
        gfx.DrawString("TAX INVOICE", titleFont, XBrushes.Black, new XRect(margin, y, pageWidth, 24), XStringFormats.TopRight);
        y += 30;
        gfx.DrawLine(XPens.Gray, margin, y, margin + pageWidth, y);
        y += 14;

        var leftX = margin;
        var rightX = margin + pageWidth * 0.6;
        var blockTop = y;

        gfx.DrawString("Bill To", labelFont, XBrushes.Gray, new XPoint(leftX, y + 10));
        y += 14;
        gfx.DrawString(string.IsNullOrWhiteSpace(snapshot.CustomerName) ? "Walk-in Customer" : snapshot.CustomerName, valueFont, XBrushes.Black, new XPoint(leftX, y + 10));
        y += 14;
        if (!string.IsNullOrWhiteSpace(snapshot.CustomerAddress))
        {
            gfx.DrawString(snapshot.CustomerAddress, cellFont, XBrushes.Black, new XPoint(leftX, y + 10));
            y += 14;
        }
        var customerRegNumbers = string.Join("   ", new[]
        {
            string.IsNullOrWhiteSpace(snapshot.CustomerPanNumber) ? null : $"PAN: {snapshot.CustomerPanNumber}",
            string.IsNullOrWhiteSpace(snapshot.CustomerVatNumber) ? null : $"VAT: {snapshot.CustomerVatNumber}"
        }.Where(s => s != null));
        if (!string.IsNullOrWhiteSpace(customerRegNumbers))
        {
            gfx.DrawString(customerRegNumbers, cellFont, XBrushes.Black, new XPoint(leftX, y + 10));
            y += 14;
        }

        var infoY = blockTop;
        void DrawInfoLine(string label, string value)
        {
            gfx.DrawString(label, labelFont, XBrushes.Gray, new XPoint(rightX, infoY + 10));
            gfx.DrawString(value, valueFont, XBrushes.Black, new XRect(rightX + 90, infoY, pageWidth * 0.4 - 90, 14), XStringFormats.TopLeft);
            infoY += 14;
        }

        DrawInfoLine("Invoice No:", snapshot.InvoiceNumber);
        DrawInfoLine("Date:", snapshot.InvoiceDate.ToString("yyyy-MM-dd") + (string.IsNullOrWhiteSpace(snapshot.InvoiceDateBs) ? "" : $" (BS {snapshot.InvoiceDateBs})"));
        DrawInfoLine("Due Date:", snapshot.DueDate.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(snapshot.Reference))
            DrawInfoLine("Reference:", snapshot.Reference);

        y = Math.Max(y, infoY) + 16;

        var columnCount = 5;
        var weights = new[] { 3.0, 1.0, 1.0, 1.0, 1.2 };
        var totalWeight = weights.Sum();
        var columnOffsets = new double[columnCount + 1];
        for (var i = 0; i < columnCount; i++)
            columnOffsets[i + 1] = columnOffsets[i] + pageWidth * weights[i] / totalWeight;
        var headers = new[] { "Description", "Qty", "Rate", "Disc%", "Amount" };
        const double rowHeight = 18;
        double pageHeight = page.Height.Point;

        void DrawCell(int col, string text, XFont font, bool rightAlign)
        {
            var colWidth = columnOffsets[col + 1] - columnOffsets[col];
            var rect = new XRect(margin + columnOffsets[col] + 3, y + 2, colWidth - 6, rowHeight - 4);
            gfx.Save();
            gfx.IntersectClip(rect);
            gfx.DrawString(text, font, XBrushes.Black, rect, rightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
            gfx.Restore();
        }

        void EnsureSpace()
        {
            if (y + rowHeight > pageHeight - margin - 80)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
            }
        }

        EnsureSpace();
        gfx.DrawRectangle(XBrushes.WhiteSmoke, margin, y, pageWidth, rowHeight);
        for (var c = 0; c < headers.Length; c++)
            DrawCell(c, headers[c], headerFont, rightAlign: c > 0);
        gfx.DrawLine(XPens.Gray, margin, y + rowHeight, margin + pageWidth, y + rowHeight);
        y += rowHeight;

        foreach (var line in snapshot.Lines)
        {
            EnsureSpace();
            DrawCell(0, line.Description, cellFont, rightAlign: false);
            DrawCell(1, line.Quantity.ToString("N2", CultureInfo.InvariantCulture), cellFont, rightAlign: true);
            DrawCell(2, line.Rate.ToString("N2", CultureInfo.InvariantCulture), cellFont, rightAlign: true);
            DrawCell(3, line.DiscountPercent.ToString("N2", CultureInfo.InvariantCulture), cellFont, rightAlign: true);
            DrawCell(4, line.Amount.ToString("N2", CultureInfo.InvariantCulture), cellFont, rightAlign: true);
            gfx.DrawLine(XPens.LightGray, margin, y + rowHeight, margin + pageWidth, y + rowHeight);
            y += rowHeight;
        }

        y += 10;
        var totalsX = margin + pageWidth * 0.6;
        var totalsWidth = pageWidth * 0.4;

        void DrawTotalLine(string label, decimal amount, XFont font)
        {
            gfx.DrawString(label, font, XBrushes.Black, new XRect(totalsX, y, totalsWidth * 0.5, 16), XStringFormats.TopLeft);
            gfx.DrawString(amount.ToString("N2", CultureInfo.InvariantCulture), font, XBrushes.Black, new XRect(totalsX + totalsWidth * 0.5, y, totalsWidth * 0.5, 16), XStringFormats.TopRight);
            y += 16;
        }

        DrawTotalLine("Subtotal", snapshot.SubTotal, cellFont);
        if (snapshot.DiscountAmount > 0)
            DrawTotalLine("Discount", snapshot.DiscountAmount, cellFont);
        if (snapshot.IsVatApplicable)
            DrawTotalLine($"VAT ({snapshot.VatRate:N0}%)", snapshot.VatAmount, cellFont);
        gfx.DrawLine(XPens.Gray, totalsX, y, margin + pageWidth, y);
        y += 4;
        DrawTotalLine("Total", snapshot.TotalAmount, totalFont);

        if (!string.IsNullOrWhiteSpace(snapshot.Narration))
        {
            y += 16;
            gfx.DrawString("Notes", labelFont, XBrushes.Gray, new XPoint(margin, y + 10));
            y += 14;
            gfx.DrawString(snapshot.Narration, cellFont, XBrushes.Black, new XRect(margin, y, pageWidth, 40), XStringFormats.TopLeft);
        }

        document.Save(filePath);
    }

    partial void OnIsVatApplicableChanged(bool value) => CalculateTotals();
    partial void OnVatRateChanged(decimal value) => CalculateTotals();

    private async Task SaveInvoiceAsync(bool post)
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        if (_savedInvoiceId.HasValue)
        {
            if (!post)
            {
                StatusMessage = IsPosted
                    ? $"Invoice {InvoiceNumber} is already posted. Select New Invoice to start another."
                    : $"Draft {InvoiceNumber} is already saved. Post it or select New Invoice.";
                return;
            }

            if (IsPosted)
            {
                StatusMessage = $"Invoice {InvoiceNumber} is already posted. Select New Invoice to start another.";
                return;
            }

            try
            {
                var postedInvoice = await _salesService.PostInvoiceAsync(_savedInvoiceId.Value, _session.UserId);
                IsPosted = true;
                CanPost = false;
                StatusMessage = $"Posted {postedInvoice.InvoiceNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }

            return;
        }

        try
        {
            CalculateTotals();
            var validLines = Lines.Where(l =>
                !string.IsNullOrWhiteSpace(l.Description) &&
                l.Quantity > 0 &&
                l.Rate > 0).ToList();

            if (validLines.Count == 0)
                throw new InvalidOperationException("Add at least one invoice line with description, quantity, and rate.");

            var customer = await _dataService.GetOrCreateCustomerAsync(_session.CompanyId.Value, CustomerName, _session.UserId);
            var savedDueDate = DueDate;
            var savedReference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim();
            var savedNarration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim();
            var savedVatApplicable = IsVatApplicable;
            var savedVatRate = VatRate;

            var invoice = await _salesService.CreateInvoiceAsync(new CreateSalesInvoiceRequest
            {
                CompanyId = _session.CompanyId.Value,
                CustomerId = customer.Id,
                InvoiceDate = InvoiceDate,
                DueDate = savedDueDate,
                Reference = savedReference,
                Narration = savedNarration,
                IsVatApplicable = savedVatApplicable,
                VatRate = savedVatRate,
                Lines = validLines.Select(l => new SalesInvoiceLineRequest
                {
                    ItemId = l.ItemId,
                    Description = l.Description.Trim(),
                    Quantity = l.Quantity,
                    Rate = l.Rate,
                    DiscountPercent = l.DiscountPercent,
                    TaxPercent = savedVatApplicable ? l.TaxPercent : 0
                }).ToList()
            }, _session.UserId);

            var lineSnapshots = invoice.Lines.Select(line =>
            {
                var grossAmount = line.Quantity * line.Rate;
                var discountPercent = grossAmount == 0 ? 0 : line.DiscountAmount * 100 / grossAmount;
                return new InvoicePrintLineSnapshot(line.Description, line.Quantity, line.Rate, discountPercent, grossAmount);
            }).ToList();

            _savedInvoiceId = invoice.Id;
            _printSnapshot = new InvoicePrintSnapshot(
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                BikramSambatConverter.IsSupported(invoice.InvoiceDate) ? BikramSambatConverter.ToBsDisplayString(invoice.InvoiceDate) : "",
                savedDueDate,
                string.IsNullOrWhiteSpace(invoice.CustomerName) ? customer.Name : invoice.CustomerName,
                customer.Address,
                customer.PanNumber,
                customer.VatNumber,
                savedReference,
                savedNarration,
                invoice.SubTotal,
                invoice.DiscountAmount,
                invoice.VatAmount,
                invoice.TotalAmount,
                savedVatApplicable,
                savedVatRate,
                lineSnapshots);

            InvoiceNumber = invoice.InvoiceNumber;
            InvoiceDate = invoice.InvoiceDate;
            CustomerName = _printSnapshot.CustomerName;
            SubTotal = invoice.SubTotal;
            DiscountAmount = invoice.DiscountAmount;
            VatAmount = invoice.VatAmount;
            TotalAmount = invoice.TotalAmount;
            IsDocumentEditable = false;
            CanPrint = true;
            CanPost = true;
            IsPosted = false;

            if (post)
            {
                var postedInvoice = await _salesService.PostInvoiceAsync(invoice.Id, _session.UserId);
                IsPosted = true;
                CanPost = false;
                StatusMessage = $"Posted {postedInvoice.InvoiceNumber}.";
            }
            else
            {
                StatusMessage = $"Saved draft {invoice.InvoiceNumber}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void CalculateTotals()
    {
        SubTotal = Lines.Sum(l => l.Amount);
        DiscountAmount = Lines.Sum(l => l.DiscountAmount);
        var taxable = SubTotal - DiscountAmount;
        VatAmount = IsVatApplicable ? taxable * VatRate / 100 : 0;
        TotalAmount = taxable + VatAmount;
    }

    private void RecalculateLineNumbers()
    {
        for (int i = 0; i < Lines.Count; i++)
            Lines[i].LineNumber = i + 1;
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InvoiceLineViewModel.Quantity)
            or nameof(InvoiceLineViewModel.Rate)
            or nameof(InvoiceLineViewModel.DiscountPercent)
            or nameof(InvoiceLineViewModel.TaxPercent))
        {
            CalculateTotals();
        }
    }

    private sealed record InvoicePrintSnapshot(
        string InvoiceNumber,
        DateTime InvoiceDate,
        string InvoiceDateBs,
        DateTime DueDate,
        string CustomerName,
        string? CustomerAddress,
        string? CustomerPanNumber,
        string? CustomerVatNumber,
        string? Reference,
        string? Narration,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal VatAmount,
        decimal TotalAmount,
        bool IsVatApplicable,
        decimal VatRate,
        IReadOnlyList<InvoicePrintLineSnapshot> Lines);

    private sealed record InvoicePrintLineSnapshot(
        string Description,
        decimal Quantity,
        decimal Rate,
        decimal DiscountPercent,
        decimal Amount);
}

public partial class InvoiceLineViewModel : ObservableObject
{
    [ObservableProperty]
    private int _lineNumber;

    [ObservableProperty]
    private Item? _selectedItem;

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _rate;

    [ObservableProperty]
    private decimal _discountPercent;

    [ObservableProperty]
    private decimal _taxPercent = 13;

    public Guid? ItemId => SelectedItem?.Id;

    public decimal Amount => Quantity * Rate;
    public decimal DiscountAmount => Amount * DiscountPercent / 100;
    public decimal NetAmount => Amount - DiscountAmount;
    public decimal TaxAmount => NetAmount * TaxPercent / 100;
    public decimal LineTotal => NetAmount + TaxAmount;

    partial void OnSelectedItemChanged(Item? value)
    {
        if (value != null)
        {
            Description = value.Name;
            Rate = value.SalePrice;
        }
    }

    partial void OnQuantityChanged(decimal value) => NotifyCalculatedFields();
    partial void OnRateChanged(decimal value) => NotifyCalculatedFields();
    partial void OnDiscountPercentChanged(decimal value) => NotifyCalculatedFields();
    partial void OnTaxPercentChanged(decimal value) => NotifyCalculatedFields();

    private void NotifyCalculatedFields()
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(NetAmount));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(LineTotal));
    }
}
