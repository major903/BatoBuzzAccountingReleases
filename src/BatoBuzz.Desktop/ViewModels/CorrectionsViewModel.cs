using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace BatoBuzz.Desktop.ViewModels;

public partial class CorrectionsViewModel : ObservableObject
{
    private readonly ISalesService _salesService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAccountingService _accountingService;
    private readonly DesktopSession _session;

    [ObservableProperty] private ObservableCollection<SalesInvoiceDto> _invoices = new();
    [ObservableProperty] private ObservableCollection<ReceiptDto> _receipts = new();
    [ObservableProperty] private ObservableCollection<PurchaseBillDto> _bills = new();
    [ObservableProperty] private ObservableCollection<PaymentDto> _payments = new();
    [ObservableProperty] private ObservableCollection<JournalEntryDto> _journals = new();
    [ObservableProperty] private SalesInvoiceDto? _selectedInvoice;
    [ObservableProperty] private ReceiptDto? _selectedReceipt;
    [ObservableProperty] private PurchaseBillDto? _selectedBill;
    [ObservableProperty] private PaymentDto? _selectedPayment;
    [ObservableProperty] private JournalEntryDto? _selectedJournal;
    [ObservableProperty] private DateTime _correctionDate = DateTime.Today;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public CorrectionsViewModel(
        ISalesService salesService,
        IPurchaseService purchaseService,
        IAccountingService accountingService,
        DesktopSession session)
    {
        _salesService = salesService;
        _purchaseService = purchaseService;
        _accountingService = accountingService;
        _session = session;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        IsBusy = true;
        try
        {
            var companyId = _session.CompanyId.Value;
            var invoicesTask = _salesService.GetInvoicesAsync(companyId);
            var receiptsTask = _salesService.GetReceiptsAsync(companyId);
            var billsTask = _purchaseService.GetBillsAsync(companyId);
            var paymentsTask = _purchaseService.GetPaymentsAsync(companyId);
            var journalsTask = _accountingService.GetJournalsAsync(companyId);
            await Task.WhenAll(invoicesTask, receiptsTask, billsTask, paymentsTask, journalsTask);

            Invoices = new ObservableCollection<SalesInvoiceDto>(invoicesTask.Result);
            Receipts = new ObservableCollection<ReceiptDto>(receiptsTask.Result);
            Bills = new ObservableCollection<PurchaseBillDto>(billsTask.Result);
            Payments = new ObservableCollection<PaymentDto>(paymentsTask.Result);
            Journals = new ObservableCollection<JournalEntryDto>(journalsTask.Result
                .Where(journal => journal.VoucherType is BatoBuzz.Contracts.Common.VoucherTypeDto.Journal
                    or BatoBuzz.Contracts.Common.VoucherTypeDto.Contra
                    or BatoBuzz.Contracts.Common.VoucherTypeDto.OpeningBalance
                    or BatoBuzz.Contracts.Common.VoucherTypeDto.DebitNote
                    or BatoBuzz.Contracts.Common.VoucherTypeDto.CreditNote));
            StatusMessage = "Select a posted document, enter a reason, and use the matching correction action.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelInvoiceAsync() => await ExecuteAsync(
        SelectedInvoice?.InvoiceNumber,
        "cancel this sales invoice",
        request => _salesService.CancelInvoiceAsync(
            SelectedInvoice?.Id ?? throw new InvalidOperationException("Select a sales invoice."),
            request,
            _session.UserId));

    [RelayCommand]
    private async Task ReverseReceiptAsync() => await ExecuteAsync(
        SelectedReceipt?.ReceiptNumber,
        "reverse this receipt",
        request => _salesService.ReverseReceiptAsync(
            SelectedReceipt?.Id ?? throw new InvalidOperationException("Select a receipt."),
            request,
            _session.UserId));

    [RelayCommand]
    private async Task CancelBillAsync() => await ExecuteAsync(
        SelectedBill?.BillNumber,
        "cancel this purchase bill",
        request => _purchaseService.CancelBillAsync(
            SelectedBill?.Id ?? throw new InvalidOperationException("Select a purchase bill."),
            request,
            _session.UserId));

    [RelayCommand]
    private async Task ReversePaymentAsync() => await ExecuteAsync(
        SelectedPayment?.PaymentNumber,
        "reverse this payment",
        request => _purchaseService.ReversePaymentAsync(
            SelectedPayment?.Id ?? throw new InvalidOperationException("Select a payment."),
            request,
            _session.UserId));

    [RelayCommand]
    private async Task ReverseJournalAsync() => await ExecuteAsync(
        SelectedJournal?.EntryNumber,
        "reverse this journal",
        request => _accountingService.ReverseJournalAsync(
            SelectedJournal?.Id ?? throw new InvalidOperationException("Select a journal."),
            request,
            _session.UserId));

    private async Task ExecuteAsync<T>(
        string? documentNumber,
        string action,
        Func<CorrectPostedDocumentRequest, Task<T>> correction)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(documentNumber))
                throw new InvalidOperationException("Select the document to correct first.");
            if (string.IsNullOrWhiteSpace(Reason))
                throw new InvalidOperationException("A correction reason is required.");
            if (MessageBox.Show(
                    $"Permanently {action} ({documentNumber}) by posting a dated reversal?",
                    "Confirm accounting correction",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            await correction(new CorrectPostedDocumentRequest
            {
                CorrectionDate = CorrectionDate.Date,
                Reason = Reason.Trim()
            });
            Reason = "";
            StatusMessage = $"Correction posted for {documentNumber}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
