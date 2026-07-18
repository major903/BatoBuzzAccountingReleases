using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfSharp.Drawing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace BatoBuzz.Desktop.ViewModels;

public partial class PurchaseBillViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;
    private readonly ICompanyService _companyService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;
    private Guid? _savedBillId;
    private PurchaseBillPrintSnapshot? _printSnapshot;

    [ObservableProperty]
    private string _billNumber = "";

    [ObservableProperty]
    private string _supplierInvoiceNumber = "";

    [ObservableProperty]
    private DateTime _billDate = DateTime.Now;

    [ObservableProperty]
    private string _billDateBs = BikramSambatConverter.IsSupported(DateTime.Now) ? BikramSambatConverter.ToBsDisplayString(DateTime.Now) : "";

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Now.AddDays(30);

    [ObservableProperty]
    private string _supplierName = "";

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
    private ObservableCollection<PurchaseLineViewModel> _lines = new();

    [ObservableProperty]
    private ObservableCollection<Item> _availableItems = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableSuppliers = new();

    [ObservableProperty]
    private ObservableCollection<PurchaseBillDto> _draftBills = new();

    [ObservableProperty]
    private PurchaseBillDto? _selectedDraft;

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

    public PurchaseBillViewModel(IPurchaseService purchaseService, ICompanyService companyService, DesktopDataService dataService, DesktopSession session)
    {
        _purchaseService = purchaseService;
        _companyService = companyService;
        _dataService = dataService;
        _session = session;
        AddLine();
        _ = LoadDataAsync();
    }

    partial void OnBillDateChanged(DateTime value) =>
        BillDateBs = BikramSambatConverter.IsSupported(value) ? BikramSambatConverter.ToBsDisplayString(value) : "";

    private async Task LoadDataAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        var items = await _dataService.GetItemsAsync(_session.CompanyId.Value);
        AvailableItems = new ObservableCollection<Item>(items.Where(i => i.IsActive).OrderBy(i => i.Name));

        var suppliers = await _dataService.GetSuppliersAsync(_session.CompanyId.Value);
        AvailableSuppliers = new ObservableCollection<string>(suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => s.Name));

        var bills = await _purchaseService.GetBillsAsync(_session.CompanyId.Value);
        DraftBills = new ObservableCollection<PurchaseBillDto>(
            bills.Where(bill => bill.Status == BatoBuzz.Contracts.Common.BillStatusDto.Draft)
                .OrderByDescending(bill => bill.BillDate)
                .ThenByDescending(bill => bill.BillNumber));
    }

    [RelayCommand]
    private void AddLine()
    {
        if (!IsDocumentEditable)
            return;

        var line = new PurchaseLineViewModel { LineNumber = Lines.Count + 1 };
        line.PropertyChanged += OnLinePropertyChanged;
        Lines.Add(line);
        CalculateTotals();
    }

    [RelayCommand]
    private void RemoveLine(PurchaseLineViewModel line)
    {
        if (!IsDocumentEditable || Lines.Count <= 1)
            return;

        line.PropertyChanged -= OnLinePropertyChanged;
        Lines.Remove(line);
        RecalculateLineNumbers();
        CalculateTotals();
    }

    [RelayCommand]
    private async Task Save() => await SaveBillAsync(post: false);

    [RelayCommand]
    private async Task Post()
    {
        if (!CanPost)
        {
            StatusMessage = $"Bill {BillNumber} is already posted. Select New Bill to start another.";
            return;
        }

        var result = MessageBox.Show("Are you sure you want to post this bill? It cannot be edited later.", "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await SaveBillAsync(post: true);
        }
    }

    [RelayCommand]
    private void NewBill()
    {
        foreach (var line in Lines)
            line.PropertyChanged -= OnLinePropertyChanged;

        Lines.Clear();
        _savedBillId = null;
        _printSnapshot = null;
        BillNumber = "";
        SupplierInvoiceNumber = "";
        BillDate = DateTime.Now;
        DueDate = DateTime.Now.AddDays(30);
        SupplierName = "";
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
        StatusMessage = "Ready for a new bill.";
        AddLine();
    }

    [RelayCommand]
    private void LoadDraft()
    {
        if (SelectedDraft == null)
        {
            StatusMessage = "Select a saved draft to load.";
            return;
        }

        foreach (var line in Lines)
            line.PropertyChanged -= OnLinePropertyChanged;
        Lines.Clear();

        _savedBillId = SelectedDraft.Id;
        _printSnapshot = null;
        BillNumber = SelectedDraft.BillNumber;
        SupplierInvoiceNumber = SelectedDraft.SupplierInvoiceNumber ?? "";
        BillDate = SelectedDraft.BillDate;
        DueDate = SelectedDraft.DueDate;
        SupplierName = SelectedDraft.SupplierName;
        Reference = SelectedDraft.Reference ?? "";
        Narration = SelectedDraft.Narration ?? "";
        IsVatApplicable = SelectedDraft.IsVatApplicable;
        VatRate = SelectedDraft.VatRate;

        foreach (var draftLine in SelectedDraft.Lines)
        {
            var line = new PurchaseLineViewModel
            {
                LineNumber = Lines.Count + 1,
                SelectedItem = draftLine.ItemId.HasValue
                    ? AvailableItems.FirstOrDefault(item => item.Id == draftLine.ItemId.Value)
                    : null,
                Description = draftLine.Description,
                Quantity = draftLine.Quantity,
                Rate = draftLine.Rate,
                DiscountPercent = draftLine.DiscountPercent,
                TaxPercent = draftLine.TaxPercent
            };
            line.PropertyChanged += OnLinePropertyChanged;
            Lines.Add(line);
        }

        if (Lines.Count == 0)
            AddLine();

        CalculateTotals();
        IsDocumentEditable = true;
        CanPrint = false;
        CanPost = true;
        IsPosted = false;
        StatusMessage = $"Loaded draft {BillNumber}.";
    }

    [RelayCommand]
    private async Task DiscardDraft()
    {
        var draft = SelectedDraft ?? DraftBills.FirstOrDefault(bill => bill.Id == _savedBillId);
        if (draft == null)
        {
            StatusMessage = "Select or load a draft to discard.";
            return;
        }

        if (MessageBox.Show(
                $"Discard draft {draft.BillNumber}? This cannot be undone.",
                "Discard Draft", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            await _purchaseService.DeleteDraftBillAsync(draft.Id, _session.UserId);
            NewBill();
            await LoadDataAsync();
            StatusMessage = $"Discarded draft {draft.BillNumber}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Print()
    {
        var snapshot = _printSnapshot;
        if (!_savedBillId.HasValue || snapshot is null)
        {
            StatusMessage = "Save the bill before printing.";
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

            var fileSafeNumber = snapshot.BillNumber;
            var tempPath = Path.Combine(Path.GetTempPath(), $"BatoBuzz-PurchaseBill-{fileSafeNumber}-{Guid.NewGuid():N}.pdf");
            WriteBillPdf(company, snapshot, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            StatusMessage = $"Opened purchase bill for printing ({fileSafeNumber}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not print bill: {ex.Message}";
        }
    }

    private void WriteBillPdf(CompanyDto company, PurchaseBillPrintSnapshot snapshot, string filePath)
    {
        var doc = new VoucherPdfDocument();

        doc.DrawText(company.TradingName ?? company.Name, VoucherPdfDocument.HeadingFont, XBrushes.Black);
        doc.Y += 20;
        var companyDetails = string.Join(", ", new[] { company.Address, company.City, company.Province }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(companyDetails))
        {
            doc.DrawText(companyDetails, VoucherPdfDocument.LabelFont, XBrushes.Black);
            doc.Y += 14;
        }
        var companyRegNumbers = string.Join("   ", new[]
        {
            string.IsNullOrWhiteSpace(company.PanNumber) ? null : $"PAN: {company.PanNumber}",
            string.IsNullOrWhiteSpace(company.VatNumber) ? null : $"VAT: {company.VatNumber}"
        }.Where(s => s != null));
        if (!string.IsNullOrWhiteSpace(companyRegNumbers))
        {
            doc.DrawText(companyRegNumbers, VoucherPdfDocument.LabelFont, XBrushes.Black);
            doc.Y += 14;
        }

        doc.Y += 12;
        doc.DrawText("PURCHASE BILL", VoucherPdfDocument.TitleFont, XBrushes.Black, width: doc.PageWidth, format: XStringFormats.TopRight);
        doc.Y += 30;
        doc.DrawRule();
        doc.Y += 14;

        var rightX = doc.PageWidth * 0.6;
        var blockTop = doc.Y;

        doc.DrawText("Supplier", VoucherPdfDocument.LabelFont, XBrushes.Gray);
        doc.Y += 14;
        doc.DrawText(string.IsNullOrWhiteSpace(snapshot.SupplierName) ? "Unnamed Supplier" : snapshot.SupplierName, VoucherPdfDocument.ValueFont, XBrushes.Black);
        doc.Y += 14;
        if (!string.IsNullOrWhiteSpace(snapshot.SupplierAddress))
        {
            doc.DrawText(snapshot.SupplierAddress, VoucherPdfDocument.CellFont, XBrushes.Black);
            doc.Y += 14;
        }
        var supplierRegNumbers = string.Join("   ", new[]
        {
            string.IsNullOrWhiteSpace(snapshot.SupplierPanNumber) ? null : $"PAN: {snapshot.SupplierPanNumber}",
            string.IsNullOrWhiteSpace(snapshot.SupplierVatNumber) ? null : $"VAT: {snapshot.SupplierVatNumber}"
        }.Where(s => s != null));
        if (!string.IsNullOrWhiteSpace(supplierRegNumbers))
        {
            doc.DrawText(supplierRegNumbers, VoucherPdfDocument.CellFont, XBrushes.Black);
            doc.Y += 14;
        }

        var infoY = blockTop;
        void DrawInfoLine(string label, string value)
        {
            doc.Y = infoY;
            doc.DrawText(label, VoucherPdfDocument.LabelFont, XBrushes.Gray, x: rightX, width: 90);
            doc.DrawText(value, VoucherPdfDocument.ValueFont, XBrushes.Black, x: rightX + 90, width: doc.PageWidth * 0.4 - 90);
            infoY += 14;
        }

        DrawInfoLine("Bill No:", snapshot.BillNumber);
        DrawInfoLine("Date:", snapshot.BillDate.ToString("yyyy-MM-dd"));
        DrawInfoLine("Due Date:", snapshot.DueDate.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(snapshot.SupplierInvoiceNumber))
            DrawInfoLine("Supplier Ref:", snapshot.SupplierInvoiceNumber);

        doc.Y = Math.Max(doc.Y, infoY) + 16;

        var headers = new[] { "Description", "Qty", "Rate", "Disc%", "Amount" };
        var weights = new[] { 3.0, 1.0, 1.0, 1.0, 1.2 };
        var rightAlign = new[] { false, true, true, true, true };
        var offsets = doc.DrawTableHeader(headers, weights);

        foreach (var line in snapshot.Lines)
        {
            doc.DrawTableRow(offsets, new[]
            {
                line.Description,
                line.Quantity.ToString("N2", CultureInfo.InvariantCulture),
                line.Rate.ToString("N2", CultureInfo.InvariantCulture),
                line.DiscountPercent.ToString("N2", CultureInfo.InvariantCulture),
                line.NetAmount.ToString("N2", CultureInfo.InvariantCulture)
            }, rightAlign, VoucherPdfDocument.CellFont);
        }

        doc.Y += 10;
        var totalsX = doc.PageWidth * 0.6;
        var totalsWidth = doc.PageWidth * 0.4;

        void DrawTotalLine(string label, decimal amount, XFont font)
        {
            doc.DrawText(label, font, XBrushes.Black, x: totalsX, width: totalsWidth * 0.5);
            doc.DrawText(amount.ToString("N2", CultureInfo.InvariantCulture), font, XBrushes.Black, x: totalsX + totalsWidth * 0.5, width: totalsWidth * 0.5, format: XStringFormats.TopRight);
            doc.Y += 16;
        }

        DrawTotalLine("Subtotal", snapshot.SubTotal, VoucherPdfDocument.CellFont);
        if (snapshot.DiscountAmount > 0)
            DrawTotalLine("Discount", snapshot.DiscountAmount, VoucherPdfDocument.CellFont);
        if (snapshot.IsVatApplicable)
            DrawTotalLine($"VAT ({snapshot.VatRate:N0}%)", snapshot.VatAmount, VoucherPdfDocument.CellFont);
        doc.DrawRule();
        doc.Y += 4;
        DrawTotalLine("Total", snapshot.TotalAmount, VoucherPdfDocument.TotalFont);

        if (!string.IsNullOrWhiteSpace(snapshot.Narration))
        {
            doc.Y += 16;
            doc.DrawText("Notes", VoucherPdfDocument.LabelFont, XBrushes.Gray);
            doc.Y += 14;
            doc.DrawText(snapshot.Narration, VoucherPdfDocument.CellFont, XBrushes.Black, width: doc.PageWidth);
        }

        doc.Save(filePath);
    }

    private async Task SaveBillAsync(bool post)
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        if (_savedBillId.HasValue)
        {
            if (IsPosted)
            {
                StatusMessage = $"Bill {BillNumber} is already posted. Select New Bill to start another.";
                return;
            }

            try
            {
                CalculateTotals();
                var validLines = Lines.Where(l =>
                    !string.IsNullOrWhiteSpace(l.Description) && l.Quantity > 0 && l.Rate > 0).ToList();
                if (validLines.Count == 0)
                    throw new InvalidOperationException("Add at least one bill line with description, quantity, and rate.");

                var supplier = await _dataService.GetOrCreateSupplierAsync(_session.CompanyId.Value, SupplierName, _session.UserId);
                var savedBill = await _purchaseService.UpdateDraftBillAsync(_savedBillId.Value, new CreatePurchaseBillRequest
                {
                    CompanyId = _session.CompanyId.Value,
                    SupplierId = supplier.Id,
                    BillDate = BillDate,
                    DueDate = DueDate,
                    SupplierInvoiceNumber = string.IsNullOrWhiteSpace(SupplierInvoiceNumber) ? null : SupplierInvoiceNumber.Trim(),
                    Reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim(),
                    Narration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim(),
                    IsVatApplicable = IsVatApplicable,
                    VatRate = VatRate,
                    Lines = validLines.Select(l => new PurchaseBillLineRequest
                    {
                        ItemId = l.ItemId,
                        Description = l.Description.Trim(),
                        Quantity = l.Quantity,
                        Rate = l.Rate,
                        DiscountPercent = l.DiscountPercent,
                        TaxPercent = IsVatApplicable ? l.TaxPercent : 0
                    }).ToList()
                }, _session.UserId);

                BillNumber = savedBill.BillNumber;
                SubTotal = savedBill.SubTotal;
                DiscountAmount = savedBill.DiscountAmount;
                VatAmount = savedBill.VatAmount;
                TotalAmount = savedBill.TotalAmount;

                if (post)
                {
                    var postedBill = await _purchaseService.PostBillAsync(_savedBillId.Value, _session.UserId);
                    IsPosted = true;
                    CanPost = false;
                    IsDocumentEditable = false;
                    StatusMessage = $"Posted {postedBill.BillNumber}.";
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = $"Updated draft {BillNumber}.";
                    await LoadDataAsync();
                }
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
            var validLines = Lines.Where(l => !string.IsNullOrWhiteSpace(l.Description) && l.Quantity > 0 && l.Rate > 0).ToList();
            if (validLines.Count == 0)
                throw new InvalidOperationException("Add at least one bill line.");

            var supplier = await _dataService.GetOrCreateSupplierAsync(_session.CompanyId.Value, SupplierName, _session.UserId);
            var savedDueDate = DueDate;
            var savedSupplierInvoiceNumber = string.IsNullOrWhiteSpace(SupplierInvoiceNumber) ? null : SupplierInvoiceNumber.Trim();
            var savedReference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim();
            var savedNarration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim();
            var savedVatApplicable = IsVatApplicable;
            var savedVatRate = VatRate;

            var bill = await _purchaseService.CreateBillAsync(new CreatePurchaseBillRequest
            {
                CompanyId = _session.CompanyId.Value,
                SupplierId = supplier.Id,
                BillDate = BillDate,
                DueDate = savedDueDate,
                SupplierInvoiceNumber = savedSupplierInvoiceNumber,
                Reference = savedReference,
                Narration = savedNarration,
                IsVatApplicable = savedVatApplicable,
                VatRate = savedVatRate,
                Lines = validLines.Select(l => new PurchaseBillLineRequest
                {
                    ItemId = l.ItemId,
                    Description = l.Description.Trim(),
                    Quantity = l.Quantity,
                    Rate = l.Rate,
                    DiscountPercent = l.DiscountPercent,
                    TaxPercent = savedVatApplicable ? l.TaxPercent : 0
                }).ToList()
            }, _session.UserId);

            var savedDiscountAmount = bill.SubTotal + bill.VatAmount - bill.TotalAmount;
            var lineSnapshots = validLines.Select(line => new PurchaseBillPrintLineSnapshot(
                line.Description.Trim(),
                line.Quantity,
                line.Rate,
                line.DiscountPercent,
                line.NetAmount)).ToList();

            _savedBillId = bill.Id;
            _printSnapshot = new PurchaseBillPrintSnapshot(
                bill.BillNumber,
                bill.BillDate,
                BikramSambatConverter.IsSupported(bill.BillDate) ? BikramSambatConverter.ToBsDisplayString(bill.BillDate) : "",
                savedDueDate,
                string.IsNullOrWhiteSpace(bill.SupplierName) ? supplier.Name : bill.SupplierName,
                supplier.Address,
                supplier.PanNumber,
                supplier.VatNumber,
                savedSupplierInvoiceNumber,
                savedReference,
                savedNarration,
                bill.SubTotal,
                savedDiscountAmount,
                bill.VatAmount,
                bill.TotalAmount,
                savedVatApplicable,
                savedVatRate,
                lineSnapshots);

            BillNumber = bill.BillNumber;
            BillDate = bill.BillDate;
            SupplierName = _printSnapshot.SupplierName;
            SubTotal = bill.SubTotal;
            DiscountAmount = savedDiscountAmount;
            VatAmount = bill.VatAmount;
            TotalAmount = bill.TotalAmount;
            IsDocumentEditable = false;
            CanPrint = true;
            CanPost = true;
            IsPosted = false;

            if (post)
            {
                var postedBill = await _purchaseService.PostBillAsync(bill.Id, _session.UserId);
                IsPosted = true;
                CanPost = false;
                StatusMessage = $"Posted {postedBill.BillNumber}.";
            }
            else
            {
                StatusMessage = $"Saved draft {bill.BillNumber}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnIsVatApplicableChanged(bool value) => CalculateTotals();
    partial void OnVatRateChanged(decimal value) => CalculateTotals();

    private void CalculateTotals()
    {
        SubTotal = Lines.Sum(l => l.Amount);
        DiscountAmount = Lines.Sum(l => l.DiscountAmount);
        VatAmount = IsVatApplicable ? Lines.Sum(l => l.NetAmount * l.TaxPercent / 100) : 0;
        TotalAmount = SubTotal - DiscountAmount + VatAmount;
    }

    private void RecalculateLineNumbers()
    {
        for (var i = 0; i < Lines.Count; i++)
            Lines[i].LineNumber = i + 1;
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PurchaseLineViewModel.Quantity)
            or nameof(PurchaseLineViewModel.Rate)
            or nameof(PurchaseLineViewModel.DiscountPercent)
            or nameof(PurchaseLineViewModel.TaxPercent))
        {
            CalculateTotals();
        }
    }

    private sealed record PurchaseBillPrintSnapshot(
        string BillNumber,
        DateTime BillDate,
        string BillDateBs,
        DateTime DueDate,
        string SupplierName,
        string? SupplierAddress,
        string? SupplierPanNumber,
        string? SupplierVatNumber,
        string? SupplierInvoiceNumber,
        string? Reference,
        string? Narration,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal VatAmount,
        decimal TotalAmount,
        bool IsVatApplicable,
        decimal VatRate,
        IReadOnlyList<PurchaseBillPrintLineSnapshot> Lines);

    private sealed record PurchaseBillPrintLineSnapshot(
        string Description,
        decimal Quantity,
        decimal Rate,
        decimal DiscountPercent,
        decimal NetAmount);
}

public partial class PurchaseLineViewModel : ObservableObject
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

    partial void OnSelectedItemChanged(Item? value)
    {
        if (value != null)
        {
            Description = value.Name;
            Rate = value.StandardCost;
        }
    }

    partial void OnQuantityChanged(decimal value) => NotifyCalculatedFields();
    partial void OnRateChanged(decimal value) => NotifyCalculatedFields();
    partial void OnDiscountPercentChanged(decimal value) => NotifyCalculatedFields();

    private void NotifyCalculatedFields()
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(NetAmount));
    }
}
