using System.Windows.Controls;
using BatoBuzz.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace BatoBuzz.Desktop.Views;

public partial class ReportsView : UserControl
{
    private bool _isCoreWebView2Ready;
    private bool _hasReport;

    public ReportsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isCoreWebView2Ready)
            return;

        try
        {
            await ReportBrowser.EnsureCoreWebView2Async();
            _isCoreWebView2Ready = true;

            // The command that generates the report can run before this control
            // finishes loading, so pick up whatever HTML is already on the
            // view model instead of waiting for a PropertyChanged notification
            // that may never come (and won't, if the content is unchanged).
            if (DataContext is ReportsViewModel viewModel && !string.IsNullOrEmpty(viewModel.ReportHtml))
            {
                ReportBrowser.CoreWebView2.NavigateToString(viewModel.ReportHtml);
                _hasReport = true;
            }
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "Microsoft Edge WebView2 Runtime is required to display reports. Please install it from https://developer.microsoft.com/microsoft-edge/webview2/.",
                "BatoBuzz Reports", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReportsViewModel oldViewModel)
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is ReportsViewModel newViewModel)
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReportsViewModel.ReportHtml) || sender is not ReportsViewModel viewModel)
            return;

        if (!_isCoreWebView2Ready)
            return;

        ReportBrowser.CoreWebView2.NavigateToString(viewModel.ReportHtml);
        _hasReport = true;
    }

    private void PrintReport_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_hasReport || !_isCoreWebView2Ready)
            {
                MessageBox.Show("Generate a report before printing.", "BatoBuzz Reports",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ReportBrowser.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print could not be opened.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "BatoBuzz Reports",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
