using CommunityToolkit.Mvvm.ComponentModel;

namespace BatoBuzz.Desktop.ViewModels;

/// <summary>One outstanding document that can be selected for a receipt or payment.</summary>
public partial class OutstandingDocumentAllocationViewModel : ObservableObject
{
    public Guid DocumentId { get; init; }
    public string DocumentNumber { get; init; } = "";
    public DateTime DocumentDate { get; init; }
    public decimal BalanceDue { get; init; }

    [ObservableProperty]
    private string _amountText = "";

    public decimal Amount => decimal.TryParse(
        AmountText,
        System.Globalization.NumberStyles.Number,
        System.Globalization.CultureInfo.CurrentCulture,
        out var value) ? value : 0;
}
