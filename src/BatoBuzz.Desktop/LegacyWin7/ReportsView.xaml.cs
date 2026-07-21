using BatoBuzz.Desktop.ViewModels;
using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BatoBuzz.Desktop.Views;

/// <summary>
/// Windows 7 cannot run the supported WebView2 runtime. This native fallback
/// keeps report generation and export available without embedding an obsolete browser.
/// </summary>
public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReportsViewModel oldViewModel)
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is ReportsViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            RenderReport(newViewModel.ReportHtml);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportsViewModel.ReportHtml) && sender is ReportsViewModel viewModel)
            RenderReport(viewModel.ReportHtml);
    }

    private void RenderReport(string html)
    {
        ReportText.Text = string.IsNullOrWhiteSpace(html)
            ? "Generate a report to view its text summary. For a formatted and printable copy, choose PDF or Excel."
            : HtmlToText(html);
    }

    private static string HtmlToText(string html)
    {
        var normalized = Regex.Replace(html, "</t[dh]>", "\t", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, "</tr>", Environment.NewLine, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, "</h[1-6]>", Environment.NewLine + Environment.NewLine, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, "<br\\s*/?>", Environment.NewLine, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, "<.*?>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(normalized).Trim();
    }
}
