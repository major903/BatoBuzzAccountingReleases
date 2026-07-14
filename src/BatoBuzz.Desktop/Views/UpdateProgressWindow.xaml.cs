using BatoBuzz.Desktop.Services;
using System.Globalization;
using System.Windows;

namespace BatoBuzz.Desktop.Views;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow(Window? owner)
    {
        InitializeComponent();
        Owner = owner;
    }

    public void Report(GitHubReleaseUpdateService.UpdateDownloadProgress progress)
    {
        StatusText.Text = progress.Status;
        DownloadProgressBar.IsIndeterminate = !progress.Percent.HasValue;
        if (progress.Percent.HasValue)
            DownloadProgressBar.Value = progress.Percent.Value;

        DetailsText.Text = progress.TotalBytes is > 0
            ? $"{FormatBytes(progress.DownloadedBytes)} of {FormatBytes(progress.TotalBytes.Value)} ({progress.Percent ?? 0}%)"
            : progress.Percent.HasValue
                ? $"{progress.Percent.Value}% complete"
                : "";
    }

    private static string FormatBytes(long value)
    {
        const double bytesPerMegabyte = 1024d * 1024d;
        return $"{value / bytesPerMegabyte:N1} MB";
    }
}
