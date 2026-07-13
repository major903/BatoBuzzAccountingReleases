using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;
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

public partial class JournalEntryViewModel : ObservableObject
{
    private readonly IAccountingService _accountingService;
    private readonly ICompanyService _companyService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;
    private Guid? _savedJournalId;
    private JournalPrintSnapshot? _printSnapshot;

    public string[] VoucherTypes { get; } = { "Journal", "Contra", "Opening Balance", "Debit Note", "Credit Note" };

    [ObservableProperty]
    private string _entryNumber = "";

    [ObservableProperty]
    private DateTime _entryDate = DateTime.Now;

    [ObservableProperty]
    private string _entryDateBs = BikramSambatConverter.IsSupported(DateTime.Now) ? BikramSambatConverter.ToBsDisplayString(DateTime.Now) : "";

    [ObservableProperty]
    private string _voucherType = "Journal";

    [ObservableProperty]
    private string _referenceNumber = "";

    [ObservableProperty]
    private string _narration = "";

    [ObservableProperty]
    private decimal _totalDebit;

    [ObservableProperty]
    private decimal _totalCredit;

    [ObservableProperty]
    private decimal _difference;

    [ObservableProperty]
    private bool _isBalanced = true;

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

    [ObservableProperty]
    private ObservableCollection<string> _availableLedgers = new();

    [ObservableProperty]
    private ObservableCollection<JournalLineEditViewModel> _lines = new();

    public JournalEntryViewModel(IAccountingService accountingService, ICompanyService companyService, DesktopDataService dataService, DesktopSession session)
    {
        _accountingService = accountingService;
        _companyService = companyService;
        _dataService = dataService;
        _session = session;
        AddLine();
        AddLine();
        RecalculateTotals();
        _ = LoadAvailableLedgersAsync();
    }

    private async Task LoadAvailableLedgersAsync()
    {
        if (!_session.CompanyId.HasValue) return;
        var ledgers = await _dataService.GetLedgersAsync(_session.CompanyId.Value);
        AvailableLedgers = new ObservableCollection<string>(ledgers.Where(l => l.IsActive).OrderBy(l => l.Name).Select(l => l.Name));
    }

    partial void OnEntryDateChanged(DateTime value) =>
        EntryDateBs = BikramSambatConverter.IsSupported(value) ? BikramSambatConverter.ToBsDisplayString(value) : "";

    [RelayCommand]
    private void AddLine()
    {
        if (!IsDocumentEditable)
            return;

        var line = new JournalLineEditViewModel { LineNumber = Lines.Count + 1 };
        line.PropertyChanged += OnLinePropertyChanged;
        Lines.Add(line);
        RecalculateTotals();
    }

    [RelayCommand]
    private void RemoveLine(JournalLineEditViewModel line)
    {
        if (IsDocumentEditable && Lines.Count > 2)
        {
            line.PropertyChanged -= OnLinePropertyChanged;
            Lines.Remove(line);
            RecalculateLineNumbers();
            RecalculateTotals();
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        await SaveJournalAsync(post: false);
    }

    [RelayCommand]
    private async Task Post()
    {
        if (!CanPost)
        {
            StatusMessage = $"Journal {EntryNumber} is already posted. Select New Journal to start another.";
            return;
        }

        var result = MessageBox.Show("Are you sure you want to post this journal entry? It cannot be edited later.", "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await SaveJournalAsync(post: true);
        }
    }

    [RelayCommand]
    private void NewJournal()
    {
        foreach (var line in Lines)
            line.PropertyChanged -= OnLinePropertyChanged;

        Lines.Clear();
        _savedJournalId = null;
        _printSnapshot = null;
        EntryNumber = "";
        EntryDate = DateTime.Now;
        VoucherType = "Journal";
        ReferenceNumber = "";
        Narration = "";
        TotalDebit = 0;
        TotalCredit = 0;
        Difference = 0;
        IsBalanced = true;
        IsDocumentEditable = true;
        CanPrint = false;
        CanPost = true;
        IsPosted = false;
        StatusMessage = "Ready for a new journal.";
        AddLine();
        AddLine();
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task Print()
    {
        var snapshot = _printSnapshot;
        if (!_savedJournalId.HasValue || snapshot is null)
        {
            StatusMessage = "Save the journal before printing.";
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

            var fileSafeNumber = snapshot.EntryNumber;
            var tempPath = Path.Combine(Path.GetTempPath(), $"BatoBuzz-Journal-{fileSafeNumber}-{Guid.NewGuid():N}.pdf");
            WriteJournalPdf(company, snapshot, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            StatusMessage = $"Opened journal voucher for printing ({fileSafeNumber}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not print journal voucher: {ex.Message}";
        }
    }

    private void WriteJournalPdf(CompanyDto company, JournalPrintSnapshot snapshot, string filePath)
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
        doc.DrawText($"{snapshot.VoucherType.ToUpperInvariant()} VOUCHER", VoucherPdfDocument.TitleFont, XBrushes.Black, width: doc.PageWidth, format: XStringFormats.TopRight);
        doc.Y += 30;
        doc.DrawRule();
        doc.Y += 20;

        void DrawInfoLine(string label, string value)
        {
            doc.DrawText(label, VoucherPdfDocument.LabelFont, XBrushes.Gray, width: 140);
            doc.DrawText(value, VoucherPdfDocument.ValueFont, XBrushes.Black, x: 140, width: doc.PageWidth - 140);
            doc.Y += 18;
        }

        DrawInfoLine("Voucher No:", snapshot.EntryNumber);
        DrawInfoLine("Date:", snapshot.EntryDate.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(snapshot.ReferenceNumber))
            DrawInfoLine("Reference:", snapshot.ReferenceNumber);

        doc.Y += 10;

        var headers = new[] { "Ledger Account", "Narration", "Debit", "Credit" };
        var weights = new[] { 2.0, 2.0, 1.0, 1.0 };
        var rightAlign = new[] { false, false, true, true };
        var offsets = doc.DrawTableHeader(headers, weights);

        foreach (var line in snapshot.Lines)
        {
            doc.DrawTableRow(offsets, new[]
            {
                line.LedgerName,
                line.Narration,
                line.Debit > 0 ? line.Debit.ToString("N2", CultureInfo.InvariantCulture) : "",
                line.Credit > 0 ? line.Credit.ToString("N2", CultureInfo.InvariantCulture) : ""
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

        doc.DrawRule();
        doc.Y += 4;
        DrawTotalLine("Total Debit", snapshot.TotalDebit, VoucherPdfDocument.TotalFont);
        DrawTotalLine("Total Credit", snapshot.TotalCredit, VoucherPdfDocument.TotalFont);

        if (!string.IsNullOrWhiteSpace(snapshot.Narration))
        {
            doc.Y += 10;
            doc.DrawText("Notes", VoucherPdfDocument.LabelFont, XBrushes.Gray);
            doc.Y += 14;
            doc.DrawText(snapshot.Narration, VoucherPdfDocument.CellFont, XBrushes.Black, width: doc.PageWidth);
        }

        doc.Save(filePath);
    }

    private async Task SaveJournalAsync(bool post)
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        if (_savedJournalId.HasValue)
        {
            if (!post)
            {
                StatusMessage = IsPosted
                    ? $"Journal {EntryNumber} is already posted. Select New Journal to start another."
                    : $"Draft {EntryNumber} is already saved. Post it or select New Journal.";
                return;
            }

            if (IsPosted)
            {
                StatusMessage = $"Journal {EntryNumber} is already posted. Select New Journal to start another.";
                return;
            }

            try
            {
                var postedJournal = await _accountingService.PostJournalAsync(_savedJournalId.Value, _session.UserId);
                IsPosted = true;
                CanPost = false;
                StatusMessage = $"Posted {postedJournal.EntryNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }

            return;
        }

        try
        {
            RecalculateTotals();
            if (TotalDebit != TotalCredit || TotalDebit <= 0)
                throw new InvalidOperationException("Journal must be balanced and greater than zero.");

            var requestLines = new List<JournalLineRequest>();
            foreach (var line in Lines.Where(l => l.Debit > 0 || l.Credit > 0))
            {
                if (string.IsNullOrWhiteSpace(line.LedgerName))
                    throw new InvalidOperationException("Ledger account is required on every amount line.");

                var ledger = await _dataService.GetLedgerByNameAsync(_session.CompanyId.Value, line.LedgerName);
                requestLines.Add(new JournalLineRequest
                {
                    LedgerId = ledger.Id,
                    DebitAmount = line.Debit,
                    CreditAmount = line.Credit,
                    Narration = string.IsNullOrWhiteSpace(line.Narration) ? null : line.Narration.Trim()
                });
            }

            var savedVoucherType = VoucherType;
            var savedReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? null : ReferenceNumber.Trim();
            var savedNarration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim();
            var journal = await _accountingService.CreateJournalAsync(new CreateJournalRequest
            {
                CompanyId = _session.CompanyId.Value,
                EntryDate = EntryDate,
                VoucherType = (int)MapVoucherType(savedVoucherType),
                ReferenceNumber = savedReferenceNumber,
                Narration = savedNarration,
                Lines = requestLines
            }, _session.UserId);

            var lineSnapshots = journal.Lines.Select(line => new JournalPrintLineSnapshot(
                line.LedgerName,
                line.Narration ?? "",
                line.DebitAmount,
                line.CreditAmount)).ToList();

            _savedJournalId = journal.Id;
            _printSnapshot = new JournalPrintSnapshot(
                journal.EntryNumber,
                journal.EntryDate,
                savedVoucherType,
                journal.ReferenceNumber,
                journal.Narration,
                journal.TotalDebit,
                journal.TotalCredit,
                lineSnapshots);

            EntryNumber = journal.EntryNumber;
            EntryDate = journal.EntryDate;
            ReferenceNumber = journal.ReferenceNumber ?? "";
            Narration = journal.Narration ?? "";
            TotalDebit = journal.TotalDebit;
            TotalCredit = journal.TotalCredit;
            Difference = journal.TotalDebit - journal.TotalCredit;
            IsBalanced = Difference == 0;
            IsDocumentEditable = false;
            CanPrint = true;
            CanPost = true;
            IsPosted = false;

            if (post)
            {
                var postedJournal = await _accountingService.PostJournalAsync(journal.Id, _session.UserId);
                IsPosted = true;
                CanPost = false;
                StatusMessage = $"Posted {postedJournal.EntryNumber}.";
            }
            else
            {
                StatusMessage = $"Saved draft {journal.EntryNumber}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RecalculateTotals()
    {
        TotalDebit = Lines.Sum(l => l.Debit);
        TotalCredit = Lines.Sum(l => l.Credit);
        Difference = TotalDebit - TotalCredit;
        IsBalanced = Difference == 0;
    }

    private void RecalculateLineNumbers()
    {
        for (int i = 0; i < Lines.Count; i++)
            Lines[i].LineNumber = i + 1;
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(JournalLineEditViewModel.Debit) or nameof(JournalLineEditViewModel.Credit))
            RecalculateTotals();
    }

    private static BatoBuzz.Domain.Enums.VoucherType MapVoucherType(string value) => value switch
    {
        "Contra" => BatoBuzz.Domain.Enums.VoucherType.Contra,
        "Opening Balance" => BatoBuzz.Domain.Enums.VoucherType.OpeningBalance,
        "Debit Note" => BatoBuzz.Domain.Enums.VoucherType.DebitNote,
        "Credit Note" => BatoBuzz.Domain.Enums.VoucherType.CreditNote,
        _ => BatoBuzz.Domain.Enums.VoucherType.Journal
    };

    private sealed record JournalPrintSnapshot(
        string EntryNumber,
        DateTime EntryDate,
        string VoucherType,
        string? ReferenceNumber,
        string? Narration,
        decimal TotalDebit,
        decimal TotalCredit,
        IReadOnlyList<JournalPrintLineSnapshot> Lines);

    private sealed record JournalPrintLineSnapshot(
        string LedgerName,
        string Narration,
        decimal Debit,
        decimal Credit);
}

public partial class JournalLineEditViewModel : ObservableObject
{
    [ObservableProperty]
    private int _lineNumber;

    [ObservableProperty]
    private string _ledgerName = "";

    [ObservableProperty]
    private decimal _debit;

    [ObservableProperty]
    private decimal _credit;

    [ObservableProperty]
    private string _narration = "";

    partial void OnDebitChanged(decimal value)
    {
        if (value > 0) Credit = 0;
    }

    partial void OnCreditChanged(decimal value)
    {
        if (value > 0) Debit = 0;
    }
}
