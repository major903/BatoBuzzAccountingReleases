using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Common;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfSharp.Drawing;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace BatoBuzz.Desktop.ViewModels;

public partial class ReceiptViewModel : ObservableObject, IWorkspaceDocumentState
{
    private readonly ISalesService _salesService;
    private readonly ICompanyService _companyService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;
    private Guid? _savedReceiptId;
    private ReceiptPrintSnapshot? _printSnapshot;
    private bool _suppressDirty;

    public string WorkspaceName => "Receipt";
    public bool CanEditDocument => IsDocumentEditable && !IsBusy;

    [ObservableProperty]
    private string _receiptNumber = "";

    [ObservableProperty]
    private string _customerName = "";

    [ObservableProperty]
    private string _receiptDateText = DocumentInput.FormatDate(DateTime.Now);

    [ObservableProperty]
    private string _receiptDateBs = BikramSambatConverter.IsSupported(DateTime.Now)
        ? BikramSambatConverter.ToBsDisplayString(DateTime.Now)
        : "";

    [ObservableProperty]
    private string _amountText = "";

    public decimal Amount => DocumentInput.DecimalOrZero(AmountText);

    [ObservableProperty]
    private string _paymentMethod = "Cash";

    [ObservableProperty]
    private string _bankName = "";

    [ObservableProperty]
    private string _reference = "";

    [ObservableProperty]
    private string _narration = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isDocumentEditable = true;

    [ObservableProperty]
    private bool _canPrint;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private ObservableCollection<string> _availableCustomers = new();

    public string[] PaymentMethods { get; } = { "Cash", "Cheque", "Bank Transfer", "Mobile Money", "Card" };

    public ReceiptViewModel(
        ISalesService salesService,
        ICompanyService companyService,
        DesktopDataService dataService,
        DesktopSession session)
    {
        _salesService = salesService;
        _companyService = companyService;
        _dataService = dataService;
        _session = session;
        _ = LoadAvailableCustomersAsync();
    }

    private async Task LoadAvailableCustomersAsync()
    {
        var companyId = _session.CompanyId;
        if (!companyId.HasValue)
            return;

        try
        {
            var customers = await _dataService.GetCustomersAsync(companyId.Value);
            AvailableCustomers = new ObservableCollection<string>(
                customers.Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => c.Name));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load customers: {ex.Message}";
        }
    }

    partial void OnCustomerNameChanged(string value) => MarkDirty();

    partial void OnReceiptDateTextChanged(string value)
    {
        ReceiptDateBs = DocumentInput.TryParseDate(value, out var date)
            && BikramSambatConverter.IsSupported(date)
                ? BikramSambatConverter.ToBsDisplayString(date)
                : "";
        MarkDirty();
    }

    partial void OnAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(Amount));
        MarkDirty();
    }

    partial void OnPaymentMethodChanged(string value) => MarkDirty();
    partial void OnBankNameChanged(string value) => MarkDirty();
    partial void OnReferenceChanged(string value) => MarkDirty();
    partial void OnNarrationChanged(string value) => MarkDirty();

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditDocument));
        SaveCommand.NotifyCanExecuteChanged();
        NewReceiptCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDocumentEditableChanged(bool value) =>
        OnPropertyChanged(nameof(CanEditDocument));

    partial void OnCanPrintChanged(bool value) =>
        PrintCommand.NotifyCanExecuteChanged();

    private void MarkDirty()
    {
        if (!_suppressDirty && IsDocumentEditable)
            HasUnsavedChanges = true;
    }

    private bool CanStartAction() => !IsBusy;
    private bool CanPrintAction() => !IsBusy && CanPrint;

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private async Task Save()
    {
        if (_savedReceiptId.HasValue)
        {
            StatusMessage = $"Receipt {ReceiptNumber} is already saved. Select New Receipt to enter another.";
            return;
        }

        var result = MessageBox.Show(
            "Are you sure you want to save this receipt? It will be applied to the customer's balance.",
            "Confirm Save",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        try
        {
            await SaveInternalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveInternalAsync()
    {
        try
        {
            var companyId = _session.CompanyId
                ?? throw new InvalidOperationException("No company selected.");
            var userId = _session.UserId;
            if (userId == Guid.Empty)
                throw new InvalidOperationException("Sign in before recording a receipt.");

            var customerName = CustomerName.Trim();
            if (string.IsNullOrWhiteSpace(customerName))
                throw new InvalidOperationException("Customer is required.");

            if (!DocumentInput.TryParseDate(ReceiptDateText, out var receiptDate))
                throw new InvalidOperationException("Enter a valid receipt date.");

            if (!DocumentInput.TryParseDecimal(AmountText, out var amount) || amount <= 0)
                throw new InvalidOperationException("Amount must be a valid number greater than zero.");

            var paymentMethod = PaymentMethod;
            var bankName = string.IsNullOrWhiteSpace(BankName) ? null : BankName.Trim();
            var reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim();
            var narration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim();

            var customer = await _dataService.GetOrCreateCustomerAsync(companyId, customerName, userId);

            // Allocate to the oldest outstanding invoices first; any remainder is an advance.
            var openInvoices = (await _salesService.GetInvoicesAsync(companyId))
                .Where(i => i.CustomerId == customer.Id
                    && i.BalanceDue > 0
                    && i.Status != InvoiceStatusDto.Draft
                    && i.Status != InvoiceStatusDto.Cancelled)
                .OrderBy(i => i.InvoiceDate)
                .ToList();

            var allocations = new List<ReceiptAllocationRequest>();
            var remaining = amount;
            foreach (var invoice in openInvoices)
            {
                if (remaining <= 0)
                    break;

                var applied = Math.Min(remaining, invoice.BalanceDue);
                allocations.Add(new ReceiptAllocationRequest
                {
                    SalesInvoiceId = invoice.Id,
                    AmountAllocated = applied
                });
                remaining -= applied;
            }

            var receipt = await _salesService.RecordReceiptAsync(new CreateReceiptRequest
            {
                CompanyId = companyId,
                CustomerId = customer.Id,
                ReceiptDate = receiptDate,
                Amount = amount,
                PaymentMethod = PaymentMethodToInt(paymentMethod),
                BankName = bankName,
                Reference = reference,
                Narration = narration,
                IsAdvance = remaining > 0,
                Allocations = allocations
            }, userId);

            _savedReceiptId = receipt.Id;
            _printSnapshot = new ReceiptPrintSnapshot(
                receipt.ReceiptNumber,
                receipt.ReceiptDate,
                string.IsNullOrWhiteSpace(receipt.CustomerName) ? customer.Name : receipt.CustomerName,
                receipt.Amount,
                paymentMethod,
                bankName,
                reference,
                narration);

            _suppressDirty = true;
            try
            {
                ReceiptNumber = receipt.ReceiptNumber;
                CustomerName = _printSnapshot.CustomerName;
                ReceiptDateText = DocumentInput.FormatDate(receipt.ReceiptDate);
                AmountText = DocumentInput.FormatDecimal(receipt.Amount);
                IsDocumentEditable = false;
                CanPrint = true;
            }
            finally
            {
                _suppressDirty = false;
            }

            HasUnsavedChanges = false;
            StatusMessage = $"Saved receipt {receipt.ReceiptNumber}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintAction))]
    private async Task Print()
    {
        var snapshot = _printSnapshot;
        if (!_savedReceiptId.HasValue || snapshot is null)
        {
            StatusMessage = "Save the receipt before printing.";
            return;
        }

        var companyId = _session.CompanyId;
        if (!companyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        IsBusy = true;
        try
        {
            var company = await _companyService.GetCompanyAsync(companyId.Value);
            if (company == null)
            {
                StatusMessage = "Company details could not be loaded.";
                return;
            }

            var fileSafeNumber = snapshot.ReceiptNumber;
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"BatoBuzz-Receipt-{fileSafeNumber}-{Guid.NewGuid():N}.pdf");
            WriteReceiptPdf(company, snapshot, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            StatusMessage = $"Opened receipt for printing ({fileSafeNumber}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not print receipt: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void NewReceipt()
    {
        if (HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "Discard the unsaved receipt and start a new one?",
                "Unsaved Receipt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
                return;
        }

        _suppressDirty = true;
        try
        {
            _savedReceiptId = null;
            _printSnapshot = null;
            ReceiptNumber = "";
            CustomerName = "";
            ReceiptDateText = DocumentInput.FormatDate(DateTime.Now);
            AmountText = "";
            PaymentMethod = "Cash";
            BankName = "";
            Reference = "";
            Narration = "";
            IsDocumentEditable = true;
            CanPrint = false;
        }
        finally
        {
            _suppressDirty = false;
        }

        HasUnsavedChanges = false;
        StatusMessage = "Ready for a new receipt.";
    }

    private static void WriteReceiptPdf(CompanyDto company, ReceiptPrintSnapshot snapshot, string filePath)
    {
        var doc = new VoucherPdfDocument();

        doc.DrawText(company.TradingName ?? company.Name, VoucherPdfDocument.HeadingFont, XBrushes.Black);
        doc.Y += 20;
        var companyDetails = string.Join(
            ", ",
            new[] { company.Address, company.City, company.Province }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(companyDetails))
        {
            doc.DrawText(companyDetails, VoucherPdfDocument.LabelFont, XBrushes.Black);
            doc.Y += 14;
        }

        doc.Y += 12;
        doc.DrawText(
            "RECEIPT",
            VoucherPdfDocument.TitleFont,
            XBrushes.Black,
            width: doc.PageWidth,
            format: XStringFormats.TopRight);
        doc.Y += 30;
        doc.DrawRule();
        doc.Y += 20;

        void DrawInfoLine(string label, string value)
        {
            doc.DrawText(label, VoucherPdfDocument.LabelFont, XBrushes.Gray, width: 140);
            doc.DrawText(value, VoucherPdfDocument.ValueFont, XBrushes.Black, x: 140, width: doc.PageWidth - 140);
            doc.Y += 18;
        }

        DrawInfoLine("Receipt No:", snapshot.ReceiptNumber);
        DrawInfoLine("Date:", snapshot.ReceiptDate.ToString("yyyy-MM-dd"));
        DrawInfoLine(
            "Received From:",
            string.IsNullOrWhiteSpace(snapshot.CustomerName) ? "Walk-in Customer" : snapshot.CustomerName);
        DrawInfoLine("Amount:", snapshot.Amount.ToString("N2", CultureInfo.InvariantCulture) + " NPR");
        DrawInfoLine("Payment Method:", snapshot.PaymentMethod);
        if (!string.IsNullOrWhiteSpace(snapshot.BankName))
            DrawInfoLine("Bank / Account:", snapshot.BankName);
        if (!string.IsNullOrWhiteSpace(snapshot.Reference))
            DrawInfoLine("Reference:", snapshot.Reference);

        if (!string.IsNullOrWhiteSpace(snapshot.Narration))
        {
            doc.Y += 10;
            doc.DrawText("Notes", VoucherPdfDocument.LabelFont, XBrushes.Gray);
            doc.Y += 14;
            doc.DrawText(snapshot.Narration, VoucherPdfDocument.CellFont, XBrushes.Black, width: doc.PageWidth);
        }

        doc.Save(filePath);
    }

    private static int PaymentMethodToInt(string value) => value switch
    {
        "Cheque" => 2,
        "Bank Transfer" => 3,
        "Mobile Money" => 4,
        "Card" => 5,
        _ => 1
    };

    private sealed record ReceiptPrintSnapshot(
        string ReceiptNumber,
        DateTime ReceiptDate,
        string CustomerName,
        decimal Amount,
        string PaymentMethod,
        string? BankName,
        string? Reference,
        string? Narration);
}
