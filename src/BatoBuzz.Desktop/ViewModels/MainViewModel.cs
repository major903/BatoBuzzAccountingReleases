using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Desktop.Views;
using BatoBuzz.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using System.Linq;

namespace BatoBuzz.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private readonly RememberedLoginService _rememberedLogin;
    private readonly GitHubReleaseUpdateService _updateService;
    private IServiceScope? _loginScope;
    private bool _disposed;
    private static readonly string DataDirectory = DesktopStoragePaths.DataDirectory;
    private static readonly string DatabasePath = DesktopStoragePaths.DatabasePath;
    private static readonly string PendingRestorePath = DesktopStoragePaths.PendingRestorePath;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private ObservableCollection<TabItemViewModel> _activeTabs = new();

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    [ObservableProperty]
    private string _windowTitle = "BatoBuzz Accounting";

    [ObservableProperty]
    private string _companyName = "No Company Selected";

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _currentDate = FormatCurrentDate();

    private static string FormatCurrentDate()
    {
        var today = DateTime.Now;
        var ad = today.ToString("yyyy-MM-dd");
        return BikramSambatConverter.IsSupported(today)
            ? $"{ad} ({BikramSambatConverter.ToBsDisplayString(today)} BS)"
            : ad;
    }

    [ObservableProperty]
    private bool _isAuthenticated;

    public MainViewModel(
        IServiceScopeFactory scopeFactory,
        DesktopSession session,
        RememberedLoginService rememberedLogin,
        GitHubReleaseUpdateService updateService)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        _rememberedLogin = rememberedLogin;
        _updateService = updateService;
    }

    public void Initialize()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MainViewModel));

        if (CurrentView == null && !IsAuthenticated)
            ShowLogin();
    }

    [RelayCommand]
    private void ShowLogin()
    {
        CurrentView = null;
        DisposeLoginScope();
        DisposeTabs();

        IsAuthenticated = false;
        SetUser("");
        CompanyName = "No Company Selected";
        WindowTitle = "BatoBuzz Accounting";

        var scope = _scopeFactory.CreateScope();
        try
        {
            var loginViewModel = scope.ServiceProvider.GetRequiredService<LoginViewModel>();
            _loginScope = scope;
            CurrentView = loginViewModel;
            _ = loginViewModel.InitializeAsync();
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private (TViewModel ViewModel, bool Created) NavigateToTab<TViewModel>(
        string header,
        Action<TViewModel>? configure = null)
        where TViewModel : class
    {
        var existing = ActiveTabs.FirstOrDefault(t => t.Header == header);
        if (existing != null)
        {
            SelectedTab = existing;
            if (existing.ViewModel is not TViewModel existingViewModel)
                throw new InvalidOperationException($"Tab '{header}' contains an unexpected view model.");

            return (existingViewModel, false);
        }

        var scope = _scopeFactory.CreateScope();
        try
        {
            var viewModel = scope.ServiceProvider.GetRequiredService<TViewModel>();
            configure?.Invoke(viewModel);

            var tab = new TabItemViewModel(header, viewModel, scope);
            ActiveTabs.Add(tab);
            SelectedTab = tab;
            return (viewModel, true);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    [RelayCommand]
    private void CloseTab(TabItemViewModel tab)
    {
        var wasSelected = SelectedTab == tab;
        if (!ActiveTabs.Remove(tab))
            return;

        tab.Dispose();
        if (ActiveTabs.Count == 0)
        {
            ShowDashboard();
        }
        else if (wasSelected || SelectedTab == null)
        {
            SelectedTab = ActiveTabs.LastOrDefault();
        }
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        NavigateToTab<DashboardViewModel>("Dashboard");
    }

    [RelayCommand]
    private void ShowCompanySetup()
    {
        NavigateToTab<CompanySetupViewModel>("Company Setup");
    }

    [RelayCommand]
    private void ShowChangeCompany()
    {
        NavigateToTab<ChangeCompanyViewModel>("Change Company");
    }

    [RelayCommand]
    private void ShowSalesInvoice()
    {
        NavigateToTab<SalesInvoiceViewModel>("Sales Invoice");
    }

    [RelayCommand]
    private void ShowCustomers()
    {
        NavigateToTab<CustomerViewModel>("Customers");
    }

    [RelayCommand]
    private void ShowSuppliers()
    {
        NavigateToTab<SupplierViewModel>("Suppliers");
    }

    [RelayCommand]
    private void ShowPurchaseBill()
    {
        NavigateToTab<PurchaseBillViewModel>("Purchase Bill");
    }

    [RelayCommand]
    private void ShowReceipt()
    {
        NavigateToTab<ReceiptViewModel>("Receipt");
    }

    [RelayCommand]
    private void ShowPayment()
    {
        NavigateToTab<PaymentViewModel>("Payment");
    }

    [RelayCommand]
    private void ShowInventory()
    {
        NavigateToTab<InventoryViewModel>("Inventory");
    }

    [RelayCommand]
    private void ShowJournalEntry()
    {
        NavigateToTab<JournalEntryViewModel>("Journal Entry");
    }

    [RelayCommand]
    private void ShowBankReconciliation()
    {
        NavigateToTab<BankReconciliationViewModel>("Bank Rec");
    }

    [RelayCommand]
    private void ShowCorrections()
    {
        NavigateToTab<CorrectionsViewModel>("Corrections");
    }

    [RelayCommand]
    private void ShowTaxSettings()
    {
        NavigateToTab<TaxSettingsViewModel>("Tax Settings");
    }

    [RelayCommand]
    private void ShowPeriodLockSettings()
    {
        NavigateToTab<PeriodLockSettingsViewModel>("Period Lock");
    }

    [RelayCommand]
    private void ShowChangePassword()
    {
        NavigateToTab<ChangePasswordViewModel>("Change Password");
    }

    [RelayCommand]
    private void ShowJournalEntryWithType(string voucherType)
    {
        NavigateToTab<JournalEntryViewModel>(voucherType, viewModel =>
            viewModel.VoucherType = voucherType);
    }

    [RelayCommand]
    private void ShowReports()
    {
        NavigateToTab<ReportsViewModel>("Reports");
    }

    [RelayCommand]
    private void ShowReport(string reportName)
    {
        var (viewModel, created) = NavigateToTab<ReportsViewModel>(
            $"Report: {reportName}",
            reportViewModel => reportViewModel.SelectedReport = reportName);

        if (created)
            viewModel.GenerateReportCommand.Execute(null);
    }

    [RelayCommand]
    private void Logout()
    {
        _rememberedLogin.Clear();
        _session.UserId = Guid.Empty;
        _session.UserName = "";
        _session.CompanyId = null;
        _session.CompanyName = "";
        ShowLogin();
    }

    [RelayCommand]
    private void Backup()
    {
        try
        {
            if (!File.Exists(DatabasePath))
            {
                MessageBox.Show("No local database was found to back up.", "BatoBuzz Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BatoBuzz Backups");
            Directory.CreateDirectory(backupDirectory);

            var backupPath = Path.Combine(
                backupDirectory,
                $"BatoBuzz-{DateTime.Now:yyyyMMdd-HHmmss}.db");

            SqliteDatabaseGuard.CreateValidatedBackup(DatabasePath, backupPath);

            MessageBox.Show($"Backup created successfully:{Environment.NewLine}{backupPath}", "BatoBuzz Backup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            OpenFolder(backupDirectory);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Backup failed.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "BatoBuzz Backup",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Restore()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select BatoBuzz database backup",
            Filter = "SQLite database (*.db)|*.db|All files (*.*)|*.*",
            InitialDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BatoBuzz Backups")
        };

        if (dialog.ShowDialog() != true)
            return;

        string selectedBackupPath;
        try
        {
            selectedBackupPath = Path.GetFullPath(dialog.FileName);
            if (string.Equals(selectedBackupPath, Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The active database cannot be selected as its own restore source.");

            SqliteDatabaseGuard.ValidateBackup(selectedBackupPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The selected file is not a valid BatoBuzz backup.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "BatoBuzz Restore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            $"This will replace all current company data with the selected backup:{Environment.NewLine}{selectedBackupPath}{Environment.NewLine}{Environment.NewLine}" +
            "Your current data is automatically saved as a safety copy first, but the app will close immediately to apply the restore. Continue?",
            "BatoBuzz Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            Directory.CreateDirectory(DataDirectory);
            SqliteDatabaseGuard.ValidateBackup(selectedBackupPath);
            File.WriteAllText(PendingRestorePath, selectedBackupPath);

            MessageBox.Show(
                "Restore is staged. The application will close now. Open BatoBuzz again to complete the restore.",
                "BatoBuzz Restore",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore could not be staged.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "BatoBuzz Restore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ShowUserGuide()
    {
        var root = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(root, "docs", "QUICK_START.md"),
            Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "RUN_AND_BUILD_WINDOWS.md")),
            Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "README.md")),
            Path.Combine(root, "RUN_AND_BUILD_WINDOWS.md"),
            Path.Combine(root, "README.md")
        };

        var guide = candidates.FirstOrDefault(File.Exists);
        if (guide != null)
        {
            Process.Start(new ProcessStartInfo(guide) { UseShellExecute = true });
            return;
        }

        MessageBox.Show(
            "Quick guide: create or sign in to an owner account, complete Company Setup, then use the top menus for Accounting, Sales, Purchase, Inventory, and Reports.",
            "BatoBuzz User Guide",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ShowAbout()
    {
        MessageBox.Show(
            $"BatoBuzz Accounting v{GetCurrentVersion()}" + Environment.NewLine +
            "Offline-first Windows accounting software for Nepal." + Environment.NewLine + Environment.NewLine +
            "Includes company setup, customers, suppliers, sales, purchase, receipts, payments, journals, inventory, dashboard, reports, backup, restore, and tester-controlled updates.",
            "About BatoBuzz Accounting",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            var update = await _updateService.GetAvailableUpdateAsync(GetCurrentVersion());
            if (update is null)
            {
                MessageBox.Show(
                    $"You are using the latest version (v{GetCurrentVersion()}).",
                    "BatoBuzz Updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var install = MessageBox.Show(
                $"BatoBuzz Accounting v{update.Version} is available.{Environment.NewLine}{Environment.NewLine}" +
                $"What's new:{Environment.NewLine}{update.ReleaseNotes}{Environment.NewLine}{Environment.NewLine}" +
                "Download, verify, and install this update now?",
                "BatoBuzz Updates",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                MessageBoxResult.Yes);
            if (install != MessageBoxResult.Yes)
                return;

            var mainWindow = System.Windows.Application.Current.MainWindow;
            var progressWindow = new UpdateProgressWindow(mainWindow);
            var progress = new Progress<GitHubReleaseUpdateService.UpdateDownloadProgress>(progressWindow.Report);
            if (mainWindow is not null)
                mainWindow.IsEnabled = false;

            progressWindow.Show();
            string installerPath;
            try
            {
                installerPath = await _updateService.DownloadAndVerifyInstallerAsync(update, progress);
            }
            finally
            {
                progressWindow.Close();
                if (mainWindow is not null)
                {
                    mainWindow.IsEnabled = true;
                    mainWindow.Activate();
                }
            }
            MessageBox.Show(
                "The update was downloaded and verified. The installer will open now; BatoBuzz will close so the update can finish.",
                "BatoBuzz Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to check for or install updates.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "BatoBuzz Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }
    }

    [RelayCommand]
    private void Exit()
    {
        System.Windows.Application.Current.Shutdown();
    }

    public void SetSession(Guid userId, string userName, Guid? companyId, string? companyName)
    {
        CurrentView = null;
        DisposeLoginScope();
        DisposeTabs();

        _session.UserId = userId;
        _session.UserName = userName;
        _session.CompanyId = companyId;
        _session.CompanyName = companyName ?? "";
        IsAuthenticated = true;
        SetUser(userName);

        if (companyId.HasValue && companyId.Value != Guid.Empty)
        {
            SetCompany(companyName ?? "Company");
            _rememberedLogin.UpdateLastCompany(userId, companyId.Value);
            ShowDashboard();
        }
        else
        {
            CompanyName = "Company setup required";
            WindowTitle = "BatoBuzz Accounting - Company Setup";
            ShowCompanySetup();
        }
    }

    public void SetCompany(string name)
    {
        CompanyName = name;
        WindowTitle = $"BatoBuzz Accounting - {name}";
    }

    public void SetUser(string name)
    {
        UserName = name;
    }

    private static void OpenFolder(string folder)
    {
        if (Directory.Exists(folder))
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    public string ApplicationVersion => $"v{GetCurrentVersion()}";

    private static Version GetCurrentVersion()
    {
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version
            ?? typeof(MainViewModel).Assembly.GetName().Version
            ?? new Version(1, 0, 0);
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }

    private void DisposeLoginScope()
    {
        _loginScope?.Dispose();
        _loginScope = null;
    }

    private void DisposeTabs()
    {
        var tabs = ActiveTabs.ToArray();
        SelectedTab = null;
        ActiveTabs.Clear();

        foreach (var tab in tabs)
            tab.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CurrentView = null;
        DisposeLoginScope();
        DisposeTabs();
        GC.SuppressFinalize(this);
    }
}

public partial class TabItemViewModel : ObservableObject, IDisposable
{
    private IServiceScope? _scope;

    [ObservableProperty]
    private string _header = "";

    [ObservableProperty]
    private object _viewModel;

    public TabItemViewModel(string header, object viewModel, IServiceScope scope)
    {
        _header = header;
        _viewModel = viewModel;
        _scope = scope;
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _scope = null;
        GC.SuppressFinalize(this);
    }
}
