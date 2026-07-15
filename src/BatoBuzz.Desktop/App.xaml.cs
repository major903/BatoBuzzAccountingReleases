using BatoBuzz.Desktop.ViewModels;
using BatoBuzz.Desktop.Views;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace BatoBuzz.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private static readonly string DataDirectory = DesktopStoragePaths.DataDirectory;
    private static readonly string LogDirectory = Path.Combine(DataDirectory, "logs");
    private static readonly string StartupLogPath = Path.Combine(LogDirectory, "startup-error.log");
    private static readonly string DatabasePath = DesktopStoragePaths.DatabasePath;
    private static readonly string PendingRestorePath = DesktopStoragePaths.PendingRestorePath;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(LogDirectory);
            ApplyPendingRestore();

            _host = Host.CreateDefaultBuilder()
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContext<BatoBuzzDbContext>(options =>
                        options.UseSqlite($"Data Source={DatabasePath};Cache=Shared"));

                    services.AddScoped<BatoBuzz.Application.Interfaces.IUnitOfWork, BatoBuzz.Infrastructure.Persistence.UnitOfWork>();
                    services.AddScoped<BatoBuzz.Application.Services.ITokenService, BatoBuzz.Application.Services.LocalTokenService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.IAuthService, BatoBuzz.Application.Services.AuthService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.ICompanyService, BatoBuzz.Application.Services.CompanyService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.IAccountingService, BatoBuzz.Application.Services.AccountingService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.ISalesService, BatoBuzz.Application.Services.SalesService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.IPurchaseService, BatoBuzz.Application.Services.PurchaseService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.IInventoryService, BatoBuzz.Application.Services.InventoryService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.IDashboardService, BatoBuzz.Application.Services.DashboardService>();
                    services.AddScoped<BatoBuzz.Application.Interfaces.ITdsService, BatoBuzz.Application.Services.TdsService>();

                    services.AddSingleton<DesktopSession>();
                    services.AddSingleton<RememberedLoginService>();
                    services.AddSingleton<GitHubReleaseUpdateService>();
                    services.AddSingleton<AutomaticBackupService>();
                    services.AddScoped<DesktopDataService>();

                    // MainViewModel must be a singleton: it's the shell bound to the actual
                    // window, and several child ViewModels (CompanySetupViewModel, etc.) take
                    // it as a constructor dependency to call SetSession/navigation commands on.
                    // A transient registration here silently hands those children a disconnected
                    // MainViewModel instance -- SetSession still mutates the real (singleton)
                    // DesktopSession, so data operations proceed against the new company, but the
                    // visible header/title/CurrentView never update, since they live on the
                    // orphaned instance nobody is watching.
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<CompanySetupViewModel>();
                    services.AddTransient<ChangeCompanyViewModel>();
                    services.AddTransient<SalesInvoiceViewModel>();
                    services.AddTransient<CustomerViewModel>();
                    services.AddTransient<SupplierViewModel>();
                    services.AddTransient<PurchaseBillViewModel>();
                    services.AddTransient<ReceiptViewModel>();
                    services.AddTransient<PaymentViewModel>();
                    services.AddTransient<InventoryViewModel>();
                    services.AddTransient<JournalEntryViewModel>();
                    services.AddTransient<ReportsViewModel>();
                    services.AddTransient<TaxSettingsViewModel>();
                    services.AddTransient<PeriodLockSettingsViewModel>();
                    services.AddTransient<BankReconciliationViewModel>();
                    services.AddTransient<ChangePasswordViewModel>();
                    services.AddTransient<CorrectionsViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            using (var scope = _host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BatoBuzzDbContext>();
                db.Database.EnsureCreated();

                var connectionString = db.Database.GetConnectionString();
                if (!string.IsNullOrEmpty(connectionString))
                    SchemaUpgrader.ApplyAll(connectionString);
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            HandleFatalException("Application startup failed", ex);
        }
    }

    private static void WriteExceptionLog(string heading, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var text = new StringBuilder()
                .AppendLine(new string('=', 80))
                .AppendLine($"UTC: {DateTime.UtcNow:O}")
                .AppendLine(heading)
                .AppendLine(exception.ToString())
                .ToString();
            File.AppendAllText(StartupLogPath, text);
        }
        catch
        {
            // Never allow logging failures to hide the original exception.
        }
    }

    private static void ApplyPendingRestore()
    {
        if (!File.Exists(PendingRestorePath))
            return;

        string selectedBackupPath;
        try
        {
            selectedBackupPath = Path.GetFullPath(File.ReadAllText(PendingRestorePath).Trim());
            if (string.Equals(selectedBackupPath, DatabasePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The active database cannot be selected as its own restore source.");

            SqliteDatabaseGuard.ValidateBackup(selectedBackupPath);
        }
        catch
        {
            DeletePendingRestoreMarker();
            throw;
        }

        var stagingPath = Path.Combine(
            DataDirectory,
            $".restore-staging-{Guid.NewGuid():N}.db");
        string? safetyBackupPath = null;
        var replacementStarted = false;

        try
        {
            SqliteDatabaseGuard.CreateOnlineBackup(selectedBackupPath, stagingPath);

            if (File.Exists(DatabasePath))
            {
                safetyBackupPath = Path.Combine(
                    DataDirectory,
                    $"BatoBuzz-before-restore-{DateTime.Now:yyyyMMdd-HHmmssfff}.db");
                SqliteDatabaseGuard.CreateOnlineBackup(DatabasePath, safetyBackupPath);
            }

            SqliteConnection.ClearAllPools();
            replacementStarted = true;
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var sidecarPath = DatabasePath + suffix;
                if (File.Exists(sidecarPath))
                    File.Delete(sidecarPath);
            }

            if (File.Exists(DatabasePath))
                File.Delete(DatabasePath);

            File.Move(stagingPath, DatabasePath);
            SqliteDatabaseGuard.ValidateBackup(DatabasePath);
            File.Delete(PendingRestorePath);
        }
        catch
        {
            SqliteConnection.ClearAllPools();

            try
            {
                if (File.Exists(stagingPath))
                    File.Delete(stagingPath);

                if (replacementStarted && safetyBackupPath != null && File.Exists(safetyBackupPath))
                {
                    foreach (var suffix in new[] { "", "-wal", "-shm" })
                    {
                        var targetPath = DatabasePath + suffix;
                        if (File.Exists(targetPath))
                            File.Delete(targetPath);
                    }

                    File.Copy(safetyBackupPath, DatabasePath, overwrite: true);
                }
            }
            catch (Exception recoveryException)
            {
                WriteExceptionLog("Automatic restore rollback failed", recoveryException);
            }

            DeletePendingRestoreMarker();
            throw;
        }
    }

    private static void DeletePendingRestoreMarker()
    {
        try
        {
            if (File.Exists(PendingRestorePath))
                File.Delete(PendingRestorePath);
        }
        catch
        {
            // The original restore error remains the actionable failure.
        }
    }

    private void HandleFatalException(string heading, Exception exception)
    {
        WriteExceptionLog(heading, exception);
        MessageBox.Show(
            $"{heading}.{Environment.NewLine}{Environment.NewLine}" +
            $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"Technical details were saved to:{Environment.NewLine}{StartupLogPath}",
            "BatoBuzz Accounting",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteExceptionLog("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred.{Environment.NewLine}{Environment.NewLine}" +
            $"{e.Exception.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"Log file:{Environment.NewLine}{StartupLogPath}",
            "BatoBuzz Accounting",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            WriteExceptionLog("Unhandled application exception", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteExceptionLog("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
