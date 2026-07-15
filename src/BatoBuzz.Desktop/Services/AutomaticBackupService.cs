using System.IO;

namespace BatoBuzz.Desktop.Services;

/// <summary>
/// Creates one validated local database backup per calendar day while BatoBuzz is in use.
/// Automatic backups are stored separately from operator-created backups and retained for two weeks.
/// </summary>
public sealed class AutomaticBackupService
{
    private const int RetainedBackupCount = 14;
    private readonly string _databasePath;
    private readonly string _automaticBackupDirectory;

    public AutomaticBackupService(
        string? databasePath = null,
        string? automaticBackupDirectory = null)
    {
        _databasePath = databasePath ?? DesktopStoragePaths.DatabasePath;
        _automaticBackupDirectory = automaticBackupDirectory ?? DesktopStoragePaths.AutomaticBackupDirectory;
    }

    public AutomaticBackupResult EnsureDailyBackup()
    {
        var now = DateTime.Now;
        if (!File.Exists(_databasePath))
        {
            return AutomaticBackupResult.NotRequired(
                "Automatic backup will start after the local database is created.");
        }

        Directory.CreateDirectory(_automaticBackupDirectory);
        var existingBackups = GetAutomaticBackups();
        if (existingBackups.Any(backup => backup.LastWriteTime.Date == now.Date))
        {
            return AutomaticBackupResult.Current(existingBackups
                .OrderByDescending(backup => backup.LastWriteTime)
                .First()
                .FullName);
        }

        var backupPath = Path.Combine(
            _automaticBackupDirectory,
            $"BatoBuzz-Auto-{now:yyyyMMdd-HHmmss}.db");

        SqliteDatabaseGuard.CreateValidatedBackup(_databasePath, backupPath);
        PruneOldBackups();
        return AutomaticBackupResult.Created(backupPath);
    }

    private IReadOnlyList<FileInfo> GetAutomaticBackups()
    {
        if (!Directory.Exists(_automaticBackupDirectory))
            return [];

        return Directory
            .EnumerateFiles(_automaticBackupDirectory, "BatoBuzz-Auto-*.db")
            .Select(path => new FileInfo(path))
            .Where(file => file.Length > 0)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
    }

    private void PruneOldBackups()
    {
        foreach (var backup in GetAutomaticBackups().Skip(RetainedBackupCount))
        {
            try
            {
                backup.Delete();
            }
            catch (IOException)
            {
                // A locked older backup does not make today's validated backup unsafe.
            }
            catch (UnauthorizedAccessException)
            {
                // The next daily run will try retention cleanup again.
            }
        }
    }
}

public sealed record AutomaticBackupResult(
    AutomaticBackupStatus Status,
    string Message,
    string? BackupPath = null)
{
    public static AutomaticBackupResult Created(string backupPath) =>
        new(AutomaticBackupStatus.Created, "Automatic backup completed today.", backupPath);

    public static AutomaticBackupResult Current(string backupPath) =>
        new(AutomaticBackupStatus.Current, "Automatic backup is current.", backupPath);

    public static AutomaticBackupResult NotRequired(string message) =>
        new(AutomaticBackupStatus.NotRequired, message);
}

public enum AutomaticBackupStatus
{
    Created,
    Current,
    NotRequired
}
