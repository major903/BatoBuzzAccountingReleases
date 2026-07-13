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

public partial class PaymentViewModel : ObservableObject, IWorkspaceDocumentState
{
    private readonly IPurchaseService _purchaseService;
    private readonly ITdsService _tdsService;
    private readonly ICompanyService _companyService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;
    private Guid? _savedPaymentId;
    private PaymentPrintSnapshot? _printSnapshot;
    private bool _suppressDirty;
    private bool _recalculatingTds;

    public string WorkspaceName => "Payment";
    public bool CanEditDocument => IsDocumentEditable && !IsBusy;

    [ObservableProperty]
    private string _paymentNumber = "";

    [ObservableProperty]
    private string _supplierName = "";

    [ObservableProperty]
    private string _paymentDateText = DocumentInput.FormatDate(DateTime.Now);

    [ObservableProperty]
    private string _paymentDateBs = BikramSambatConverter.IsSupported(DateTime.Now) ? BikramSambatConverter.ToBsDisplayString(DateTime.Now) : "";

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
    private ObservableCollection<TdsRateListItemViewModel> _tdsRates = new();

    [ObservableProperty]
    private TdsRateListItemViewModel? _selectedTdsRate;

    [ObservableProperty]
    private string _tdsAmountText = "";

    public decimal TdsAmount => DocumentInput.DecimalOrZero(TdsAmountText);
    public decimal TotalSettled => Amount + TdsAmount;

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
    private ObservableCollection<string> _availableSuppliers = new();

    public string[] PaymentMethods { get; } = { "Cash", "Cheque", "Bank Transfer", "Mobile Money", "Card" };

    public PaymentViewModel(IPurchaseService purchaseService, ITdsService tdsService, ICompanyService companyService, DesktopDataService dataService, DesktopSession session)
    {
        _purchaseService = purchaseService;
        _tdsService = tdsService;
        _companyService = companyService;
        _dataService = dataService;
        _session = session;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        var companyId = _session.CompanyId.Value;
        StatusMessage = "Loading payment data...";
        try
        {
            await LoadTdsRatesAsync(companyId);
            await LoadAvailableSuppliersAsync(companyId);
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load payment data: {ex.Message}";
        }
    }

    private async Task LoadAvailableSuppliersAsync(Guid companyId)
    {
        var suppliers = await _dataService.GetSuppliersAsync(companyId);
        AvailableSuppliers = new ObservableCollection<string>(suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => s.Name));
    }

    partial void OnSupplierNameChanged(string value) => MarkDirty();

    partial void OnPaymentDateTextChanged(string value)
    {
        PaymentDateBs = DocumentInput.TryParseDate(value, out var date)
            && BikramSambatConverter.IsSupported(date)
                ? BikramSambatConverter.ToBsDisplayString(date)
                : "";
        MarkDirty();
    }

    partial void OnAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(Amount));
        if (SelectedTdsRate != null)
            RecalculateTds();
        OnPropertyChanged(nameof(TotalSettled));
        MarkDirty();
    }

    partial void OnSelectedTdsRateChanged(TdsRateListItemViewModel? value)
    {
        RecalculateTds();
        MarkDirty();
    }

    partial void OnTdsAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(TdsAmount));
        OnPropertyChanged(nameof(TotalSettled));
        if (!_recalculatingTds)
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
        NewPaymentCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDocumentEditableChanged(bool value) =>
        OnPropertyChanged(nameof(CanEditDocument));

    partial void OnCanPrintChanged(bool value) =>
        PrintCommand.NotifyCanExecuteChanged();

    private void RecalculateTds()
    {
        _recalculatingTds = true;
        try
        {
            if (SelectedTdsRate == null)
            {
                TdsAmountText = "";
            }
            else if (DocumentInput.TryParseDecimal(AmountText, out var amount) && amount >= 0)
            {
                TdsAmountText = DocumentInput.FormatDecimal(
                    Math.Round(amount * SelectedTdsRate.RatePercent / 100, 2));
            }
            else
            {
                TdsAmountText = "";
            }
        }
        finally
        {
            _recalculatingTds = false;
        }
    }

    private void MarkDirty()
    {
        if (!_suppressDirty && IsDocumentEditable)
            HasUnsavedChanges = true;
    }

    private bool CanStartAction() => !IsBusy;
    private bool CanPrintAction() => !IsBusy && CanPrint;

    private async Task LoadTdsRatesAsync(Guid companyId)
    {
        var rates = await _tdsService.GetRatesAsync(companyId);
        TdsRates.Clear();
        foreach (var r in rates)
        {
            TdsRates.Add(new TdsRateListItemViewModel
            {
                Id = r.Id,
                Name = r.Name,
                RatePercent = r.RatePercent,
                Description = r.Description ?? "",
                IsActive = r.IsActive
            });
        }
    }

    private async Task SaveInternalAsync()
    {
        try
        {
            if (_savedPaymentId.HasValue)
                throw new InvalidOperationException(
                    $"Payment {PaymentNumber} is already saved. Select New Payment to enter another.");

            var companyId = _session.CompanyId
                ?? throw new InvalidOperationException("No company selected.");
            var userId = _session.UserId;
            if (userId == Guid.Empty)
                throw new InvalidOperationException("Sign in before recording a payment.");

            var supplierName = SupplierName.Trim();
            if (string.IsNullOrWhiteSpace(supplierName))
                throw new InvalidOperationException("Supplier is required.");

            if (!DocumentInput.TryParseDate(PaymentDateText, out var paymentDate))
                throw new InvalidOperationException("Enter a valid payment date.");

            if (!DocumentInput.TryParseDecimal(AmountText, out var amount) || amount <= 0)
                throw new InvalidOperationException("Amount must be a valid number greater than zero.");

            decimal tdsAmount;
            if (string.IsNullOrWhiteSpace(TdsAmountText))
            {
                tdsAmount = 0;
            }
            else if (!DocumentInput.TryParseDecimal(TdsAmountText, out tdsAmount) || tdsAmount < 0)
            {
                throw new InvalidOperationException("TDS amount must be a valid non-negative number.");
            }

            var totalSettled = amount + tdsAmount;
            var paymentMethod = PaymentMethod;
            var bankName = string.IsNullOrWhiteSpace(BankName) ? null : BankName.Trim();
            var reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim();
            var narration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim();

            var supplier = await _dataService.GetOrCreateSupplierAsync(
                companyId,
                supplierName,
                userId);

            // Allocate cash plus TDS to the oldest outstanding bills. Any remainder is an advance.
            var openBills = (await _purchaseService.GetBillsAsync(companyId))
                .Where(b => b.SupplierId == supplier.Id
                    && b.BalanceDue > 0
                    && b.Status != BillStatusDto.Draft
                    && b.Status != BillStatusDto.Cancelled)
                .OrderBy(b => b.BillDate)
                .ToList();

            var allocations = new List<PaymentAllocationRequest>();
            var remaining = totalSettled;
            foreach (var bill in openBills)
            {
                if (remaining <= 0)
                    break;

                var applied = Math.Min(remaining, bill.BalanceDue);
                allocations.Add(new PaymentAllocationRequest
                {
                    PurchaseBillId = bill.Id,
                    AmountAllocated = applied
                });
                remaining -= applied;
            }

            var payment = await _purchaseService.RecordPaymentAsync(new CreatePaymentRequest
            {
                CompanyId = companyId,
                SupplierId = supplier.Id,
                PaymentDate = paymentDate,
                Amount = amount,
                TdsAmount = tdsAmount,
                PaymentMethod = PaymentMethodToInt(paymentMethod),
                BankName = bankName,
                Reference = reference,
                Narration = narration,
                IsAdvance = remaining > 0,
                Allocations = allocations
            }, userId);

            _savedPaymentId = payment.Id;
            _printSnapshot = new PaymentPrintSnapshot(
                payment.PaymentNumber,
                payment.PaymentDate,
                string.IsNullOrWhiteSpace(payment.SupplierName) ? supplier.Name : payment.SupplierName,
                payment.Amount,
                payment.TdsAmount,
                payment.Amount + payment.TdsAmount,
                paymentMethod,
                bankName,
                reference,
                narration);

            _suppressDirty = true;
            try
            {
                PaymentNumber = payment.PaymentNumber;
                SupplierName = _printSnapshot.SupplierName;
                PaymentDateText = DocumentInput.FormatDate(payment.PaymentDate);
                AmountText = DocumentInput.FormatDecimal(payment.Amount);
                TdsAmountText = payment.TdsAmount == 0
                    ? ""
                    : DocumentInput.FormatDecimal(payment.TdsAmount);
                IsDocumentEditable = false;
                CanPrint = true;
            }
            finally
            {
                _suppressDirty = false;
            }

            HasUnsavedChanges = false;
            StatusMessage = $"Saved payment {payment.PaymentNumber}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private async Task Save()
    {
        if (_savedPaymentId.HasValue)
        {
            StatusMessage = $"Payment {PaymentNumber} is already saved. Select New Payment to enter another.";
            return;
        }

        var result = MessageBox.Show(
            "Are you sure you want to save this payment? It will be applied to the supplier's balance.",
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

    [RelayCommand]
    private async Task Print()
    {
        var snapshot = _printSnapshot;
        if (!_savedPaymentId.HasValue || snapshot is null)
        {
            StatusMessage = "Save the payment before printing.";
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

            var fileSafeNumber = snapshot.PaymentNumber;
            var tempPath = Path.Combine(Path.GetTempPath(), $"BatoBuzz-Payment-{fileSafeNumber}-{Guid.NewGuid():N}.pdf");
            WritePaymentPdf(company, snapshot, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            StatusMessage = $"Opened payment voucher for printing ({fileSafeNumber}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not print payment: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NewPayment()
    {
        _savedPaymentId = null;
        _printSnapshot = null;
        PaymentNumber = "";
        SupplierName = "";
        PaymentDateText = DocumentInput.FormatDate(DateTime.Now);
        AmountText = "";
        SelectedTdsRate = null;
        TdsAmountText = "";
        PaymentMethod = "Cash";
        BankName = "";
        Reference = "";
        Narration = "";
        IsDocumentEditable = true;
        CanPrint = false;
        StatusMessage = "Ready for a new payment.";
    }

    private void WritePaymentPdf(CompanyDto company, PaymentPrintSnapshot snapshot, string filePath)
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

        doc.Y += 12;
        doc.DrawText("PAYMENT VOUCHER", VoucherPdfDocument.TitleFont, XBrushes.Black, width: doc.PageWidth, format: XStringFormats.TopRight);
        doc.Y += 30;
        doc.DrawRule();
        doc.Y += 20;

        void DrawInfoLine(string label, string value)
        {
            doc.DrawText(label, VoucherPdfDocument.LabelFont, XBrushes.Gray, width: 140);
            doc.DrawText(value, VoucherPdfDocument.ValueFont, XBrushes.Black, x: 140, width: doc.PageWidth - 140);
            doc.Y += 18;
        }

        DrawInfoLine("Voucher No:", snapshot.PaymentNumber);
        DrawInfoLine("Date:", snapshot.PaymentDate.ToString("yyyy-MM-dd"));
        DrawInfoLine("Paid To:", string.IsNullOrWhiteSpace(snapshot.SupplierName) ? "Unnamed Supplier" : snapshot.SupplierName);
        DrawInfoLine("Amount Paid:", snapshot.Amount.ToString("N2", CultureInfo.InvariantCulture) + " NPR");
        if (snapshot.TdsAmount > 0)
        {
            DrawInfoLine("TDS Withheld:", snapshot.TdsAmount.ToString("N2", CultureInfo.InvariantCulture) + " NPR");
            DrawInfoLine("Total Settled:", snapshot.TotalSettled.ToString("N2", CultureInfo.InvariantCulture) + " NPR");
        }
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

    private sealed record PaymentPrintSnapshot(
        string PaymentNumber,
        DateTime PaymentDate,
        string SupplierName,
        decimal Amount,
        decimal TdsAmount,
        decimal TotalSettled,
        string PaymentMethod,
        string? BankName,
        string? Reference,
        string? Narration);
}
