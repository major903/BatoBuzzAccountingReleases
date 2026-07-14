using System.Windows;

namespace BatoBuzz.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(string version, Window? owner)
    {
        InitializeComponent();
        Owner = owner;
        VersionText.Text = $"Version {version}";
    }
}
